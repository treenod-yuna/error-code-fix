using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ErrorAutoFixer
{
    /// <summary>
    /// Gemini API 호출 클라이언트
    /// UnityWebRequest 기반 비동기 처리 (EditorApplication.update 폴링)
    /// </summary>
    public static class GeminiAPIClient
    {
        // === 상수 ===
        private const int REQUEST_TIMEOUT = 30;     // 요청 타임아웃 (초)
        private const int MAX_RETRY_COUNT = 2;      // 최대 재시도 횟수
        private const float RETRY_DELAY = 3f;       // 재시도 대기 시간 (초)

        // === 상태 ===
        /// <summary>현재 API 호출 진행 중 여부</summary>
        public static bool IsRequesting { get; private set; }

        /// <summary>
        /// 에러 분석을 비동기적으로 요청
        /// </summary>
        /// <param name="error">분석할 에러 정보</param>
        /// <param name="sourceCode">소스 코드 (없으면 null)</param>
        /// <param name="onSuccess">성공 콜백</param>
        /// <param name="onError">실패 콜백 (에러 메시지)</param>
        public static void AnalyzeError(
            CapturedError error,
            string sourceCode,
            System.Action<AnalysisResult> onSuccess,
            System.Action<string> onError)
        {
            // API 키 확인
            if (!ErrorFixerSettings.HasAPIKey())
            {
                onError?.Invoke("API 키가 설정되지 않았습니다. Settings에서 API 키를 입력해주세요.");
                return;
            }

            // 요청 본문 구성
            string requestBody = APIRequestBuilder.BuildRequestBody(
                error.message,
                error.stackTrace,
                sourceCode,
                error.filePath,
                error.lineNumber
            );

            string url = ErrorFixerSettings.GetAPIEndpoint();

            // API 호출 (재시도 로직 포함)
            SendRequestWithRetry(url, requestBody, 0,
                responseJson =>
                {
                    // 응답 파싱
                    var result = APIResponseParser.ParseResponse(responseJson);
                    if (result != null)
                    {
                        onSuccess?.Invoke(result);
                    }
                    else
                    {
                        onError?.Invoke("API 응답을 분석할 수 없습니다. 다시 시도해주세요.");
                    }
                },
                onError
            );
        }

        /// <summary>
        /// API 키 유효성 테스트
        /// </summary>
        /// <param name="apiKey">테스트할 API 키</param>
        /// <param name="onResult">결과 콜백 (유효 여부, 메시지)</param>
        public static void TestAPIKey(string apiKey, System.Action<bool, string> onResult)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{ErrorFixerSettings.GetModelName()}:generateContent?key={apiKey}";
            string requestBody = APIRequestBuilder.BuildTestRequestBody();

            SendRequest(url, requestBody,
                responseJson =>
                {
                    onResult?.Invoke(true, "API 키가 유효합니다.");
                },
                errorMessage =>
                {
                    onResult?.Invoke(false, errorMessage);
                }
            );
        }

        /// <summary>
        /// 재시도 로직이 포함된 API 요청
        /// </summary>
        private static void SendRequestWithRetry(
            string url, string body, int retryCount,
            System.Action<string> onComplete, System.Action<string> onError)
        {
            IsRequesting = true;

            SendRequest(url, body,
                responseJson =>
                {
                    IsRequesting = false;
                    onComplete?.Invoke(responseJson);
                },
                errorMessage =>
                {
                    // 429 에러 (Rate Limit)이고 재시도 가능하면 대기 후 재시도
                    if (errorMessage.Contains("429") && retryCount < MAX_RETRY_COUNT)
                    {
                        double retryTime = EditorApplication.timeSinceStartup + RETRY_DELAY;

                        void WaitAndRetry()
                        {
                            if (EditorApplication.timeSinceStartup < retryTime) return;

                            EditorApplication.update -= WaitAndRetry;
                            SendRequestWithRetry(url, body, retryCount + 1, onComplete, onError);
                        }

                        EditorApplication.update += WaitAndRetry;
                    }
                    else
                    {
                        IsRequesting = false;
                        onError?.Invoke(errorMessage);
                    }
                }
            );
        }

        /// <summary>
        /// UnityWebRequest를 사용한 비동기 POST 요청
        /// EditorApplication.update에서 완료를 폴링
        /// </summary>
        private static void SendRequest(
            string url, string body,
            System.Action<string> onComplete, System.Action<string> onError)
        {
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes(body);

            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = REQUEST_TIMEOUT;

            var operation = request.SendWebRequest();

            // EditorApplication.update에서 완료 여부 폴링
            void CheckComplete()
            {
                if (!operation.isDone) return;

                // 폴링 해제
                EditorApplication.update -= CheckComplete;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    request.Dispose();

                    // 메인 스레드에서 콜백 호출
                    EditorApplication.delayCall += () => onComplete?.Invoke(responseText);
                }
                else
                {
                    string errorText = request.downloadHandler?.text ?? string.Empty;
                    long responseCode = request.responseCode;
                    string errorMessage;

                    // HTTP 에러 코드별 처리
                    if (responseCode == 429)
                    {
                        errorMessage = "429: API 호출 한도를 초과했습니다.";
                    }
                    else if (!string.IsNullOrEmpty(errorText))
                    {
                        errorMessage = APIResponseParser.ParseErrorResponse(errorText);
                    }
                    else
                    {
                        errorMessage = $"네트워크 오류: {request.error}";
                    }

                    request.Dispose();

                    EditorApplication.delayCall += () => onError?.Invoke(errorMessage);
                }
            }

            EditorApplication.update += CheckComplete;
        }
    }
}
