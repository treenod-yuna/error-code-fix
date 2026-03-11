using Newtonsoft.Json.Linq;

namespace ErrorAutoFixer
{
    /// <summary>
    /// Gemini API 요청 본문을 구성하는 빌더 클래스
    /// </summary>
    public static class APIRequestBuilder
    {
        // === 상수 ===
        private const float TEMPERATURE = 0.1f;
        private const string RESPONSE_MIME_TYPE = "application/json";

        /// <summary>
        /// 에러 분석 요청 JSON 문자열을 생성
        /// </summary>
        /// <param name="errorMessage">에러 메시지</param>
        /// <param name="stackTrace">스택 트레이스</param>
        /// <param name="sourceCode">소스 코드 (없으면 null)</param>
        /// <param name="filePath">파일 경로 (없으면 null)</param>
        /// <param name="lineNumber">라인 번호</param>
        /// <returns>JSON 직렬화된 요청 본문</returns>
        public static string BuildRequestBody(
            string errorMessage,
            string stackTrace,
            string sourceCode,
            string filePath,
            int lineNumber)
        {
            string userMessage = BuildUserMessage(errorMessage, stackTrace, sourceCode, filePath, lineNumber);
            string systemPrompt = BuildSystemPrompt();

            // 시스템 프롬프트 + 사용자 메시지를 하나의 텍스트로 결합
            string combinedText = systemPrompt + "\n\n---\n\n" + userMessage;

            // Gemini API 요청 형식 구성
            var requestBody = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject
                            {
                                ["text"] = combinedText
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = RESPONSE_MIME_TYPE,
                    ["temperature"] = TEMPERATURE
                }
            };

            return requestBody.ToString();
        }

        /// <summary>
        /// API 키 유효성 테스트용 간단한 요청 본문 생성
        /// </summary>
        public static string BuildTestRequestBody()
        {
            var requestBody = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject
                            {
                                ["text"] = "Hello. 간단히 'OK'라고만 응답하세요."
                            }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0.0f
                }
            };

            return requestBody.ToString();
        }

        /// <summary>
        /// 시스템 프롬프트 생성 (Unity C# 에러 분석 전문가)
        /// </summary>
        internal static string BuildSystemPrompt()
        {
            return @"당신은 Unity C# 에러 분석 전문가입니다.
사용자가 Unity 에러 로그와 소스코드를 제공하면, 에러를 분석하고 해결 방법을 안내합니다.

반드시 아래 JSON 형식으로만 응답하세요. JSON 외의 텍스트는 절대 포함하지 마세요.

{
  ""fixable"": true 또는 false,
  ""confidence"": ""high"" 또는 ""medium"" 또는 ""low"",
  ""diagnosis"": ""에러 원인을 한국어로 설명"",
  ""file"": ""에러가 발생한 파일 경로"",
  ""line"": 에러 라인 번호(숫자),
  ""solution"": ""구체적인 해결 방법을 한국어로 단계별 설명"",
  ""patch"": {
    ""original"": ""수정 전 코드 (해당 라인/블록)"",
    ""fixed"": ""수정 후 코드 (해당 라인/블록)""
  }
}

핵심 규칙:
- 소스 코드가 함께 제공되고, 단일 파일 내 수정으로 해결 가능한 경우에만 fixable: true
- 소스 코드 없이 에러 로그만 제공된 경우: 반드시 fixable: false, patch는 null
- 여러 파일에 걸친 문제이거나, 설정/프리팹/씬 관련 문제: fixable: false
- patch.original은 실제 소스코드에 존재하는 코드여야 함 (추측 금지)
- patch.fixed는 original을 대체할 수정된 코드
- diagnosis는 반드시 한국어로, 에러의 원인을 명확히 설명
- solution은 반드시 한국어로, 구체적인 해결 단계를 설명
- confidence: high(거의 확실), medium(가능성 높음), low(추측)
- fixable이 false이면 patch는 반드시 null로 설정";
        }

        /// <summary>
        /// 사용자 메시지 생성 (에러 로그 + 소스 코드 조합)
        /// </summary>
        internal static string BuildUserMessage(
            string errorMessage,
            string stackTrace,
            string sourceCode,
            string filePath,
            int lineNumber)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("## 에러 정보");
            sb.AppendLine($"**에러 메시지:** {errorMessage}");

            if (!string.IsNullOrEmpty(stackTrace))
            {
                sb.AppendLine($"\n**스택 트레이스:**\n```\n{stackTrace}\n```");
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                sb.AppendLine($"\n**파일:** {filePath}");
                if (lineNumber > 0)
                    sb.AppendLine($"**라인:** {lineNumber}");
            }

            if (!string.IsNullOrEmpty(sourceCode))
            {
                sb.AppendLine($"\n## 소스 코드\n```csharp\n{sourceCode}\n```");
            }
            else
            {
                sb.AppendLine("\n(소스 코드를 읽을 수 없습니다)");
            }

            return sb.ToString();
        }
    }
}
