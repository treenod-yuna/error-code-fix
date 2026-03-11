using System.IO;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// 소스 코드 읽기 결과
    /// </summary>
    public class SourceCodeContext
    {
        /// <summary>파일 전체 소스 코드</summary>
        public string fullSource;

        /// <summary>에러 라인 주변 코드 (위아래 각 10줄, 라인 번호 포함)</summary>
        public string surroundingCode;

        /// <summary>에러 라인의 코드</summary>
        public string errorLine;

        /// <summary>파일 존재 여부</summary>
        public bool fileExists;

        /// <summary>파일 전체 라인 수</summary>
        public int totalLines;
    }

    /// <summary>
    /// 에러 관련 C# 소스 파일을 읽는 유틸 클래스
    /// </summary>
    public static class SourceCodeReader
    {
        // === 상수 ===
        private const int CONTEXT_LINES = 10;       // 에러 라인 위아래 포함 범위
        private const int MAX_FILE_SIZE = 500000;    // 최대 파일 크기 (500KB)

        /// <summary>
        /// 지정된 파일의 소스 코드 컨텍스트를 읽어 반환
        /// </summary>
        /// <param name="filePath">Assets/... 형태의 파일 경로</param>
        /// <param name="lineNumber">에러 라인 번호 (1-based)</param>
        /// <returns>소스 코드 컨텍스트</returns>
        public static SourceCodeContext ReadContext(string filePath, int lineNumber)
        {
            var context = new SourceCodeContext();

            // Assets/ 상대 경로를 절대 경로로 변환
            string absolutePath = GetAbsolutePath(filePath);

            if (!File.Exists(absolutePath))
            {
                context.fileExists = false;
                return context;
            }

            context.fileExists = true;

            // 파일 크기 확인
            var fileInfo = new FileInfo(absolutePath);
            string[] lines;

            if (fileInfo.Length > MAX_FILE_SIZE)
            {
                // 대용량 파일: 에러 라인 주변만 읽기
                lines = File.ReadAllLines(absolutePath, System.Text.Encoding.UTF8);
                context.fullSource = null; // 대용량이므로 전체 소스 생략
            }
            else
            {
                // 일반 파일: 전체 읽기
                lines = File.ReadAllLines(absolutePath, System.Text.Encoding.UTF8);
                context.fullSource = string.Join("\n", lines);
            }

            context.totalLines = lines.Length;

            // 에러 라인 추출 (1-based → 0-based)
            int lineIndex = lineNumber - 1;
            if (lineIndex >= 0 && lineIndex < lines.Length)
            {
                context.errorLine = lines[lineIndex];
            }

            // 주변 코드 추출 (에러 라인 기준 위아래 CONTEXT_LINES줄)
            context.surroundingCode = ExtractSurroundingCode(lines, lineIndex);

            return context;
        }

        /// <summary>
        /// 파일 전체 내용을 읽어 반환
        /// </summary>
        /// <param name="filePath">Assets/... 형태의 파일 경로</param>
        /// <returns>파일 내용 문자열 (파일 없으면 null)</returns>
        public static string ReadFullFile(string filePath)
        {
            string absolutePath = GetAbsolutePath(filePath);

            if (!File.Exists(absolutePath))
                return null;

            var fileInfo = new FileInfo(absolutePath);
            if (fileInfo.Length > MAX_FILE_SIZE)
                return null;

            return File.ReadAllText(absolutePath, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Assets/ 상대 경로를 절대 경로로 변환
        /// </summary>
        private static string GetAbsolutePath(string assetPath)
        {
            // Application.dataPath는 "프로젝트경로/Assets"를 반환
            // assetPath가 "Assets/..."로 시작하면 프로젝트 루트 기준으로 결합
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath);
        }

        /// <summary>
        /// 에러 라인 주변 코드를 라인 번호와 함께 추출
        /// </summary>
        private static string ExtractSurroundingCode(string[] lines, int errorLineIndex)
        {
            if (lines == null || lines.Length == 0)
                return string.Empty;

            // 범위 계산 (배열 경계 안전 처리)
            int startLine = Mathf.Max(0, errorLineIndex - CONTEXT_LINES);
            int endLine = Mathf.Min(lines.Length - 1, errorLineIndex + CONTEXT_LINES);

            var sb = new System.Text.StringBuilder();

            for (int i = startLine; i <= endLine; i++)
            {
                // 에러 라인 표시 (>>> 마커)
                string marker = (i == errorLineIndex) ? ">>>" : "   ";
                // 라인 번호는 1-based로 표시
                sb.AppendLine($"{marker} {i + 1,4}: {lines[i]}");
            }

            return sb.ToString();
        }
    }
}
