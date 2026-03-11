using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// Unity 에디터의 에러 로그를 실시간 캡처하는 클래스
    /// [InitializeOnLoad]로 에디터 시작 시 자동 등록
    /// </summary>
    [InitializeOnLoad]
    public static class ErrorCapture
    {
        // === 상수 ===
        private const int MAX_CAPTURED_ERRORS = 100; // 최대 캡처 수

        // === 이벤트 ===
        /// <summary>새 에러가 캡처되었을 때 발생하는 이벤트</summary>
        public static event System.Action<CapturedError> OnErrorCaptured;

        /// <summary>에러 목록이 갱신되었을 때 발생하는 이벤트 (삭제 포함)</summary>
        public static event System.Action OnErrorListChanged;

        // === 내부 저장소 ===
        private static readonly List<CapturedError> capturedErrors = new List<CapturedError>();
        private static readonly HashSet<int> errorHashes = new HashSet<int>(); // 중복 필터링용

        // === Reflection 캐시 (한 번만 조회) ===
        private static System.Type logEntriesType;
        private static System.Type logEntryType;
        private static MethodInfo startMethod;
        private static MethodInfo getEntryMethod;
        private static MethodInfo endMethod;
        private static FieldInfo conditionField;
        private static FieldInfo modeField;
        private static bool reflectionInitialized;
        private static bool reflectionAvailable;

        // === 공개 프로퍼티 ===

        /// <summary>캡처된 에러 목록 (읽기 전용)</summary>
        public static IReadOnlyList<CapturedError> CapturedErrors => capturedErrors;

        /// <summary>캡처 중 여부</summary>
        public static bool IsCapturing { get; private set; }

        // === static 생성자 (InitializeOnLoad) ===
        static ErrorCapture()
        {
            // 자동 캡처 설정이 켜져 있으면 시작
            if (ErrorFixerSettings.IsAutoCaptureEnabled())
            {
                StartCapture();
            }

            // 컴파일 완료 시 자동으로 에러 목록 갱신
            CompilationPipeline.compilationFinished += OnCompilationFinished;

            // 에디터 시작 시 콘솔에 이미 있는 에러를 스캔
            EditorApplication.delayCall += ScanConsoleErrors;
        }

        // === 제어 ===

        /// <summary>캡처 시작 (이벤트 구독)</summary>
        public static void StartCapture()
        {
            if (IsCapturing) return;

            Application.logMessageReceived += OnLogReceived;
            IsCapturing = true;
        }

        /// <summary>캡처 중지 (이벤트 해제)</summary>
        public static void StopCapture()
        {
            if (!IsCapturing) return;

            Application.logMessageReceived -= OnLogReceived;
            IsCapturing = false;
        }

        /// <summary>캡처된 에러 목록 초기화</summary>
        public static void ClearErrors()
        {
            capturedErrors.Clear();
            errorHashes.Clear();
        }

        /// <summary>
        /// Unity 콘솔과 동기화: 해결된 에러 제거 + 새 에러 추가
        /// </summary>
        public static void ScanConsoleErrors()
        {
            if (!InitReflection()) return;

            try
            {
                // 1) 현재 콘솔의 에러 메시지 목록 읽기
                var consoleErrors = ReadConsoleErrorMessages();

                // 2) 콘솔에서 사라진 에러를 목록에서 제거
                RemoveResolvedErrors(consoleErrors);

                // 3) 새로 발견된 에러 추가
                foreach (string msg in consoleErrors)
                {
                    var parsedInfo = ErrorParser.Parse(msg, "");
                    AddErrorInternal(msg, "", LogType.Error, parsedInfo);
                }

                OnErrorListChanged?.Invoke();
            }
            catch (System.Exception)
            {
                // Reflection 실패 시 무시
            }
        }

        // === 내부 로직 ===

        /// <summary>
        /// 컴파일 완료 시 자동 갱신 (에러 해결 감지)
        /// </summary>
        private static void OnCompilationFinished(object obj)
        {
            // 컴파일 직후 콘솔 상태가 갱신되므로 약간 지연 후 스캔
            EditorApplication.delayCall += ScanConsoleErrors;
        }

        /// <summary>
        /// 로그 메시지 수신 콜백
        /// Error, Exception, Assert 타입만 캡처
        /// </summary>
        private static void OnLogReceived(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            var parsedInfo = ErrorParser.Parse(message, stackTrace);
            AddErrorInternal(message, stackTrace, type, parsedInfo);
        }

        /// <summary>
        /// 현재 Unity 콘솔에 있는 에러 메시지를 전부 읽어서 반환
        /// </summary>
        private static HashSet<string> ReadConsoleErrorMessages()
        {
            var errors = new HashSet<string>();

            int count = (int)startMethod.Invoke(null, null);

            for (int i = 0; i < count; i++)
            {
                var entry = System.Activator.CreateInstance(logEntryType);
                getEntryMethod.Invoke(null, new object[] { i, entry });

                string condition = conditionField.GetValue(entry) as string;
                if (string.IsNullOrEmpty(condition)) continue;

                // 에러 여부 판별
                bool isError = false;
                if (modeField != null)
                {
                    int mode = (int)modeField.GetValue(entry);
                    isError = (mode & 1) != 0;
                }

                if (!isError && condition.Contains(": error CS"))
                    isError = true;

                if (isError)
                {
                    errors.Add(condition);
                }
            }

            endMethod.Invoke(null, null);

            return errors;
        }

        /// <summary>
        /// 콘솔에서 사라진(해결된) 컴파일 에러를 목록에서 제거
        /// </summary>
        private static void RemoveResolvedErrors(HashSet<string> currentConsoleErrors)
        {
            for (int i = capturedErrors.Count - 1; i >= 0; i--)
            {
                var error = capturedErrors[i];

                // 컴파일 에러만 자동 제거 (런타임 에러는 유지)
                if (!error.isCompileError) continue;

                // 현재 콘솔에 해당 에러가 없으면 → 해결된 것이므로 제거
                if (!currentConsoleErrors.Contains(error.message))
                {
                    int hash = ComputeErrorHash(error.message, error.stackTrace);
                    errorHashes.Remove(hash);
                    capturedErrors.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 에러를 캡처 목록에 추가 (중복 체크 포함, 공통 로직)
        /// </summary>
        private static void AddErrorInternal(string message, string stackTrace, LogType logType, ParsedErrorInfo parsedInfo)
        {
            int hash = ComputeErrorHash(message, stackTrace);
            if (errorHashes.Contains(hash))
                return;

            if (capturedErrors.Count >= MAX_CAPTURED_ERRORS)
            {
                var oldest = capturedErrors[0];
                int oldHash = ComputeErrorHash(oldest.message, oldest.stackTrace);
                errorHashes.Remove(oldHash);
                capturedErrors.RemoveAt(0);
            }

            var error = new CapturedError
            {
                message = message,
                stackTrace = stackTrace,
                logType = logType,
                timestamp = System.DateTime.Now,
                filePath = parsedInfo.hasFileInfo ? parsedInfo.filePath : null,
                lineNumber = parsedInfo.lineNumber,
                errorCode = parsedInfo.errorCode,
                isCompileError = parsedInfo.isCompileError,
                isAnalyzed = false,
                analysisResult = null
            };

            capturedErrors.Add(error);
            errorHashes.Add(hash);
            OnErrorCaptured?.Invoke(error);
        }

        /// <summary>
        /// LogEntries reflection 초기화 (한 번만 실행)
        /// </summary>
        private static bool InitReflection()
        {
            if (reflectionInitialized) return reflectionAvailable;

            reflectionInitialized = true;

            try
            {
                var asm = typeof(EditorWindow).Assembly;
                logEntriesType = asm.GetType("UnityEditor.LogEntries");
                logEntryType = asm.GetType("UnityEditor.LogEntry");

                if (logEntriesType == null || logEntryType == null) return false;

                var sf = BindingFlags.Static | BindingFlags.Public;
                startMethod = logEntriesType.GetMethod("StartGettingEntries", sf);
                getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", sf);
                endMethod = logEntriesType.GetMethod("EndGettingEntries", sf);

                if (startMethod == null || getEntryMethod == null || endMethod == null) return false;

                var instFlags = BindingFlags.Instance | BindingFlags.Public;
                conditionField = logEntryType.GetField("condition", instFlags)
                              ?? logEntryType.GetField("message", instFlags);
                modeField = logEntryType.GetField("mode", instFlags);

                if (conditionField == null) return false;

                reflectionAvailable = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ComputeErrorHash(string message, string stackTrace)
        {
            string combined = (message ?? string.Empty) + (stackTrace ?? string.Empty);
            return combined.GetHashCode();
        }
    }
}
