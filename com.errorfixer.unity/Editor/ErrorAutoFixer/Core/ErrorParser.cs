using System.Text.RegularExpressions;

namespace ErrorAutoFixer
{
    /// <summary>
    /// 에러 로그 파싱 결과
    /// </summary>
    public class ParsedErrorInfo
    {
        /// <summary>추출된 파일 경로 (Assets/... 형태)</summary>
        public string filePath;

        /// <summary>추출된 라인 번호</summary>
        public int lineNumber;

        /// <summary>에러 코드 (CS0001 등, 컴파일 에러인 경우)</summary>
        public string errorCode;

        /// <summary>컴파일 에러 여부</summary>
        public bool isCompileError;

        /// <summary>파일 경로 추출 성공 여부</summary>
        public bool hasFileInfo;
    }

    /// <summary>
    /// 스택 트레이스 및 에러 메시지에서 파일/라인 정보를 추출하는 유틸 클래스
    /// </summary>
    public static class ErrorParser
    {
        // === 정규식 패턴 ===

        // 컴파일 에러 패턴: Assets/Scripts/Example.cs(16,95): error CS1002: ; expected
        private static readonly Regex COMPILE_ERROR_REGEX =
            new Regex(@"(.+\.cs)\((\d+),\d+\):\s*error\s+(CS\d+)", RegexOptions.Compiled);

        // 런타임 에러 패턴: (at Assets/Scripts/PlayerController.cs:42)
        private static readonly Regex RUNTIME_ERROR_REGEX =
            new Regex(@"\(at\s+(.+\.cs):(\d+)\)", RegexOptions.Compiled);

        // 컴파일 경고 패턴: Assets/Scripts/Example.cs(16,95): warning CS0219
        private static readonly Regex COMPILE_WARNING_REGEX =
            new Regex(@"(.+\.cs)\((\d+),\d+\):\s*warning\s+(CS\d+)", RegexOptions.Compiled);

        /// <summary>
        /// 에러 메시지와 스택 트레이스를 파싱하여 파일/라인 정보 추출
        /// </summary>
        /// <param name="message">에러 메시지</param>
        /// <param name="stackTrace">스택 트레이스</param>
        /// <returns>파싱 결과</returns>
        public static ParsedErrorInfo Parse(string message, string stackTrace)
        {
            var result = new ParsedErrorInfo();

            // 1순위: 컴파일 에러 패턴 (에러 메시지에서 추출)
            if (!string.IsNullOrEmpty(message))
            {
                var compileMatch = COMPILE_ERROR_REGEX.Match(message);
                if (compileMatch.Success)
                {
                    result.filePath = compileMatch.Groups[1].Value.Trim();
                    result.lineNumber = int.Parse(compileMatch.Groups[2].Value);
                    result.errorCode = compileMatch.Groups[3].Value;
                    result.isCompileError = true;
                    result.hasFileInfo = true;
                    return result;
                }
            }

            // 2순위: 런타임 에러 패턴 (스택 트레이스에서 추출)
            if (!string.IsNullOrEmpty(stackTrace))
            {
                var runtimeMatch = RUNTIME_ERROR_REGEX.Match(stackTrace);
                if (runtimeMatch.Success)
                {
                    result.filePath = runtimeMatch.Groups[1].Value.Trim();
                    result.lineNumber = int.Parse(runtimeMatch.Groups[2].Value);
                    result.isCompileError = false;
                    result.hasFileInfo = true;
                    return result;
                }
            }

            // 3순위: 에러 메시지에서 런타임 패턴 시도
            if (!string.IsNullOrEmpty(message))
            {
                var runtimeMatch = RUNTIME_ERROR_REGEX.Match(message);
                if (runtimeMatch.Success)
                {
                    result.filePath = runtimeMatch.Groups[1].Value.Trim();
                    result.lineNumber = int.Parse(runtimeMatch.Groups[2].Value);
                    result.isCompileError = false;
                    result.hasFileInfo = true;
                    return result;
                }
            }

            // 파일 정보를 추출하지 못한 경우
            result.hasFileInfo = false;
            return result;
        }
    }
}
