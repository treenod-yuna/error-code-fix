using System.IO;
using UnityEditor;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// 파일 수정 패치 적용 담당
    /// 코드 치환 → 파일 쓰기 → AssetDatabase 갱신
    /// </summary>
    public static class FilePatcher
    {
        /// <summary>
        /// 패치 적용 결과
        /// </summary>
        public class PatchResult
        {
            /// <summary>패치 성공 여부</summary>
            public bool success;

            /// <summary>결과 메시지 (성공/실패 사유)</summary>
            public string message;

            /// <summary>패치 대상 파일의 Assets/ 상대 경로</summary>
            public string assetPath;
        }

        /// <summary>
        /// 분석 결과의 패치를 파일에 적용
        /// 1) 원본 코드를 수정 코드로 치환 → 2) 파일 쓰기 → 3) AssetDatabase 갱신
        /// </summary>
        /// <param name="result">Gemini API 분석 결과 (fixable=true, patch 필수)</param>
        /// <returns>패치 적용 결과</returns>
        public static PatchResult ApplyPatch(AnalysisResult result)
        {
            var patchResult = new PatchResult();

            // 유효성 검증
            if (result == null || !result.fixable || result.patch == null)
            {
                patchResult.success = false;
                patchResult.message = "수정 가능한 패치 정보가 없습니다.";
                return patchResult;
            }

            if (string.IsNullOrEmpty(result.file))
            {
                patchResult.success = false;
                patchResult.message = "수정 대상 파일 경로가 없습니다.";
                return patchResult;
            }

            if (string.IsNullOrEmpty(result.patch.original) || string.IsNullOrEmpty(result.patch.fixedCode))
            {
                patchResult.success = false;
                patchResult.message = "수정 전/후 코드 정보가 불완전합니다.";
                return patchResult;
            }

            patchResult.assetPath = result.file;

            // 절대 경로 변환
            string absolutePath = GetAbsolutePath(result.file);

            if (!File.Exists(absolutePath))
            {
                patchResult.success = false;
                patchResult.message = $"파일이 존재하지 않습니다: {result.file}";
                return patchResult;
            }

            // 파일 읽기
            string currentContent;
            try
            {
                currentContent = File.ReadAllText(absolutePath, System.Text.Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                patchResult.success = false;
                patchResult.message = $"파일 읽기 실패: {ex.Message}";
                return patchResult;
            }

            // 원본 코드가 현재 파일에 존재하는지 확인
            string normalizedContent = NormalizeLineEndings(currentContent);
            string normalizedOriginal = NormalizeLineEndings(result.patch.original);

            if (!normalizedContent.Contains(normalizedOriginal))
            {
                patchResult.success = false;
                patchResult.message = "수정 전 코드가 현재 파일에서 발견되지 않습니다.\n파일이 이미 수정되었거나 API 분석 결과가 정확하지 않을 수 있습니다.";
                return patchResult;
            }

            // 1) 코드 치환 (줄바꿈 정규화된 버전으로 치환)
            string patchedContent = normalizedContent.Replace(normalizedOriginal, NormalizeLineEndings(result.patch.fixedCode));

            // 원본 파일의 줄바꿈 형식 유지
            if (currentContent.Contains("\r\n") && !patchedContent.Contains("\r\n"))
            {
                patchedContent = patchedContent.Replace("\n", "\r\n");
            }

            // 2) 파일 쓰기
            try
            {
                File.WriteAllText(absolutePath, patchedContent, System.Text.Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                patchResult.success = false;
                patchResult.message = $"파일 쓰기 실패: {ex.Message}";
                return patchResult;
            }

            // 3) AssetDatabase 갱신 (리컴파일 트리거)
            AssetDatabase.Refresh();

            patchResult.success = true;
            patchResult.message = $"패치가 성공적으로 적용되었습니다: {result.file}";
            Debug.Log($"[ErrorAutoFixer] {patchResult.message}");

            return patchResult;
        }

        // === 유틸리티 ===

        /// <summary>
        /// Assets/ 상대 경로를 절대 경로로 변환
        /// </summary>
        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, assetPath);
        }

        /// <summary>
        /// 줄바꿈 문자를 \n으로 정규화 (비교/치환 정확도 향상)
        /// </summary>
        private static string NormalizeLineEndings(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
