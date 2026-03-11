using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ErrorAutoFixer
{
    // === 데이터 모델 ===

    /// <summary>
    /// 캡처된 에러 하나를 나타내는 데이터 클래스
    /// </summary>
    [System.Serializable]
    public class CapturedError
    {
        /// <summary>에러 메시지 원문</summary>
        public string message;

        /// <summary>스택 트레이스 원문</summary>
        public string stackTrace;

        /// <summary>Unity 로그 타입 (Error, Exception, Assert)</summary>
        public LogType logType;

        /// <summary>캡처 시간</summary>
        public System.DateTime timestamp;

        /// <summary>파싱된 파일 경로 (null 가능)</summary>
        public string filePath;

        /// <summary>파싱된 라인 번호 (0이면 미확인)</summary>
        public int lineNumber;

        /// <summary>에러 코드 (CS1002 등, 컴파일 에러인 경우)</summary>
        public string errorCode;

        /// <summary>컴파일 에러 여부</summary>
        public bool isCompileError;

        /// <summary>API 분석 완료 여부</summary>
        public bool isAnalyzed;

        /// <summary>분석 결과 (null이면 미분석)</summary>
        public AnalysisResult analysisResult;
    }

    /// <summary>
    /// 패치 정보 (수정 전/후 코드)
    /// </summary>
    [System.Serializable]
    public class PatchInfo
    {
        /// <summary>수정 전 코드</summary>
        [JsonProperty("original")]
        public string original;

        /// <summary>수정 후 코드 ("fixed"는 C# 예약어이므로 JsonProperty로 매핑)</summary>
        [JsonProperty("fixed")]
        public string fixedCode;
    }

    /// <summary>
    /// Gemini API 분석 결과
    /// </summary>
    [System.Serializable]
    public class AnalysisResult
    {
        /// <summary>자동 수정 가능 여부 (Track A/B 분기)</summary>
        [JsonProperty("fixable")]
        public bool fixable;

        /// <summary>확신도 (high, medium, low)</summary>
        [JsonProperty("confidence")]
        public string confidence;

        /// <summary>에러 원인 진단 (한국어)</summary>
        [JsonProperty("diagnosis")]
        public string diagnosis;

        /// <summary>에러 파일 경로</summary>
        [JsonProperty("file")]
        public string file;

        /// <summary>에러 라인 번호</summary>
        [JsonProperty("line")]
        public int line;

        /// <summary>해결 방법 (한국어)</summary>
        [JsonProperty("solution")]
        public string solution;

        /// <summary>패치 정보 (fixable=true일 때만, 아니면 null)</summary>
        [JsonProperty("patch")]
        public PatchInfo patch;
    }

    /// <summary>
    /// Gemini API 응답을 파싱하는 유틸 클래스
    /// </summary>
    public static class APIResponseParser
    {
        /// <summary>
        /// Gemini API의 전체 응답 JSON에서 분석 결과를 추출
        /// </summary>
        /// <param name="responseJson">API 응답 전체 JSON 문자열</param>
        /// <returns>파싱된 분석 결과 (실패 시 null)</returns>
        public static AnalysisResult ParseResponse(string responseJson)
        {
            try
            {
                // Gemini 응답 구조: candidates[0].content.parts[0].text
                var response = JObject.Parse(responseJson);
                var text = response["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning("[ErrorAutoFixer] API 응답에서 텍스트를 추출할 수 없습니다.");
                    return null;
                }

                // 추출된 텍스트가 JSON이므로 AnalysisResult로 역직렬화
                var result = JsonConvert.DeserializeObject<AnalysisResult>(text);
                return result;
            }
            catch (JsonException ex)
            {
                Debug.LogWarning($"[ErrorAutoFixer] API 응답 파싱 실패: {ex.Message}");
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ErrorAutoFixer] API 응답 처리 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// API 응답에서 에러 정보를 추출 (400, 429 등)
        /// </summary>
        /// <param name="responseJson">API 에러 응답 JSON</param>
        /// <returns>사용자에게 표시할 에러 메시지</returns>
        public static string ParseErrorResponse(string responseJson)
        {
            try
            {
                var response = JObject.Parse(responseJson);
                var errorMessage = response["error"]?["message"]?.ToString();
                var errorCode = response["error"]?["code"]?.ToString();

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Rate Limit 초과 시 친절한 메시지
                    if (errorCode == "429")
                        return "API 호출 한도를 초과했습니다. 잠시 후 다시 시도해주세요.";

                    // API 키 오류
                    if (errorCode == "400" || errorCode == "403")
                        return "API 키가 유효하지 않습니다. 설정에서 API 키를 확인해주세요.";

                    return $"API 오류 ({errorCode}): {errorMessage}";
                }

                return "알 수 없는 API 오류가 발생했습니다.";
            }
            catch
            {
                return "API 응답을 처리할 수 없습니다.";
            }
        }
    }
}
