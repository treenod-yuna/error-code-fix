using UnityEditor;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// API 키, 모델 선택, 캐시 관리 등 설정 창
    /// </summary>
    public class SettingsWindow : EditorWindow
    {
        // === 내부 상태 ===
        private string apiKeyInput = string.Empty;
        private bool showAPIKey;
        private bool isTestingKey;
        private string testResultMessage = string.Empty;
        private bool? testResultSuccess;
        private bool autoCaptureEnabled;
        private int selectedModelIndex;

        /// <summary>설정 창 열기</summary>
        [MenuItem("Tools/Error Auto Fixer/Settings", priority = 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<SettingsWindow>("Error Auto Fixer 설정");
            window.minSize = new Vector2(400, 400);
        }

        private void OnEnable()
        {
            // 저장된 설정 로드
            apiKeyInput = ErrorFixerSettings.GetAPIKey();
            autoCaptureEnabled = ErrorFixerSettings.IsAutoCaptureEnabled();
            selectedModelIndex = ErrorFixerSettings.GetModelIndex();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // 제목
            EditorGUILayout.LabelField("Error Auto Fixer 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawDivider();

            // API 키 섹션
            DrawAPIKeySection();

            EditorGUILayout.Space(10);
            DrawDivider();

            // 모델 선택 섹션
            DrawModelSection();

            EditorGUILayout.Space(10);
            DrawDivider();

            // 캡처 설정 섹션
            DrawCaptureSettings();

            EditorGUILayout.Space(10);
            DrawDivider();

            // 캐시 관리 섹션
            DrawCacheSection();

            EditorGUILayout.Space(10);
            DrawDivider();

            // 정보 섹션
            DrawAboutSection();
        }

        /// <summary>API 키 입력 및 테스트 UI</summary>
        private void DrawAPIKeySection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Gemini API 키", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            // API 키 입력 필드 (비밀번호 마스킹 토글)
            if (showAPIKey)
            {
                apiKeyInput = EditorGUILayout.TextField(apiKeyInput);
            }
            else
            {
                apiKeyInput = EditorGUILayout.PasswordField(apiKeyInput);
            }

            // 보기/숨기기 토글 버튼
            if (GUILayout.Button(showAPIKey ? "숨기기" : "보기", GUILayout.Width(60)))
            {
                showAPIKey = !showAPIKey;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // 저장 버튼
            if (GUILayout.Button("저장", GUILayout.Height(25)))
            {
                ErrorFixerSettings.SetAPIKey(apiKeyInput);
                testResultMessage = "API 키가 저장되었습니다.";
                testResultSuccess = true;
            }

            // 테스트 버튼
            EditorGUI.BeginDisabledGroup(isTestingKey || string.IsNullOrEmpty(apiKeyInput));
            if (GUILayout.Button(isTestingKey ? "테스트 중..." : "연결 테스트", GUILayout.Height(25)))
            {
                TestAPIKey();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            // 테스트 결과 메시지 표시
            if (!string.IsNullOrEmpty(testResultMessage))
            {
                EditorGUILayout.Space(3);
                if (testResultSuccess == true)
                {
                    EditorGUILayout.HelpBox(testResultMessage, MessageType.Info);
                }
                else if (testResultSuccess == false)
                {
                    EditorGUILayout.HelpBox(testResultMessage, MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox(testResultMessage, MessageType.None);
                }
            }

            // API 키 발급 안내
            EditorGUILayout.Space(3);
            if (GUILayout.Button("API 키 무료 발급받기 (Google AI Studio)", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://aistudio.google.com");
            }
        }

        /// <summary>Gemini 모델 선택 UI</summary>
        private void DrawModelSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Gemini 모델 선택", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUI.BeginChangeCheck();
            selectedModelIndex = EditorGUILayout.Popup(
                "모델",
                selectedModelIndex,
                ErrorFixerSettings.MODEL_DESCRIPTIONS);
            if (EditorGUI.EndChangeCheck())
            {
                ErrorFixerSettings.SetModel(ErrorFixerSettings.AVAILABLE_MODELS[selectedModelIndex]);
            }

            EditorGUILayout.HelpBox(
                "flash: 속도/품질 균형 (추천)\n" +
                "flash-lite: 빠른 응답, 간단한 에러에 적합\n" +
                "pro: 높은 분석 품질, 일일 요청 한도 적음",
                MessageType.Info);
        }

        /// <summary>자동 캡처 설정 UI</summary>
        private void DrawCaptureSettings()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("에러 캡처 설정", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUI.BeginChangeCheck();
            autoCaptureEnabled = EditorGUILayout.Toggle("자동 에러 캡처", autoCaptureEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                ErrorFixerSettings.SetAutoCapture(autoCaptureEnabled);

                // 캡처 상태 즉시 반영
                if (autoCaptureEnabled)
                    ErrorCapture.StartCapture();
                else
                    ErrorCapture.StopCapture();
            }

            EditorGUILayout.HelpBox(
                "활성화하면 에디터에서 발생하는 에러를 자동으로 수집합니다.",
                MessageType.Info);
        }

        /// <summary>캐시 관리 UI</summary>
        private void DrawCacheSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("분석 결과 캐시", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("캐시된 항목 수", ErrorCache.Count.ToString());

            if (GUILayout.Button("캐시 초기화", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog(
                    "캐시 초기화",
                    "저장된 모든 분석 결과 캐시를 삭제합니다.\n계속하시겠습니까?",
                    "삭제", "취소"))
                {
                    ErrorCache.ClearAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "동일한 에러가 반복되면 캐시된 결과를 즉시 표시합니다.\n" +
                "분석 결과가 부정확한 경우 캐시를 초기화하세요.",
                MessageType.Info);
        }

        /// <summary>버전 및 정보 표시</summary>
        private void DrawAboutSection()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("정보", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField("버전", "0.2.0");
            EditorGUILayout.LabelField("모델", ErrorFixerSettings.GetModelName());
            EditorGUILayout.LabelField("캡처 상태", ErrorCapture.IsCapturing ? "캡처 중" : "중지됨");
            EditorGUILayout.LabelField("캡처된 에러 수", ErrorCapture.CapturedErrors.Count.ToString());
        }

        /// <summary>구분선 그리기</summary>
        private void DrawDivider()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        /// <summary>API 키 연결 테스트 실행</summary>
        private void TestAPIKey()
        {
            isTestingKey = true;
            testResultMessage = "API 키 테스트 중...";
            testResultSuccess = null;

            GeminiAPIClient.TestAPIKey(apiKeyInput, (success, message) =>
            {
                isTestingKey = false;
                testResultSuccess = success;
                testResultMessage = message;
                Repaint();
            });
        }
    }
}
