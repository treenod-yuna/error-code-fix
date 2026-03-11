using UnityEditor;

namespace ErrorAutoFixer
{
    /// <summary>
    /// API 키 및 패키지 설정을 관리하는 정적 클래스
    /// EditorPrefs를 사용하여 로컬 머신에만 저장 (보안)
    /// </summary>
    public static class ErrorFixerSettings
    {
        // === EditorPrefs 키 상수 ===
        private const string API_KEY_PREF = "ErrorAutoFixer_GeminiAPIKey";
        private const string AUTO_CAPTURE_PREF = "ErrorAutoFixer_AutoCapture";
        private const string MODEL_PREF = "ErrorAutoFixer_Model";

        // === Gemini API 상수 ===
        private const string DEFAULT_MODEL = "gemini-2.5-flash";
        private const string GEMINI_API_BASE_URL =
            "https://generativelanguage.googleapis.com/v1beta/models/";

        /// <summary>선택 가능한 Gemini 모델 목록</summary>
        public static readonly string[] AVAILABLE_MODELS = new string[]
        {
            "gemini-2.5-flash",
            "gemini-2.5-flash-lite",
            "gemini-2.5-pro"
        };

        /// <summary>모델별 설명 (UI 표시용)</summary>
        public static readonly string[] MODEL_DESCRIPTIONS = new string[]
        {
            "gemini-2.5-flash  (추천 | 분당 10회, 일일 ~250회)",
            "gemini-2.5-flash-lite  (경량 | 분당 15회, 일일 ~1,000회)",
            "gemini-2.5-pro  (고품질 | 분당 5회, 일일 ~25회)"
        };

        // === API 키 관리 ===

        /// <summary>API 키 저장</summary>
        public static void SetAPIKey(string apiKey)
        {
            EditorPrefs.SetString(API_KEY_PREF, apiKey ?? string.Empty);
        }

        /// <summary>저장된 API 키 조회 (없으면 빈 문자열)</summary>
        public static string GetAPIKey()
        {
            return EditorPrefs.GetString(API_KEY_PREF, string.Empty);
        }

        /// <summary>API 키 설정 여부 확인</summary>
        public static bool HasAPIKey()
        {
            return !string.IsNullOrEmpty(GetAPIKey());
        }

        // === 자동 캡처 설정 ===

        /// <summary>에러 자동 캡처 ON/OFF 설정</summary>
        public static void SetAutoCapture(bool enabled)
        {
            EditorPrefs.SetBool(AUTO_CAPTURE_PREF, enabled);
        }

        /// <summary>자동 캡처 활성화 여부 조회 (기본값: true)</summary>
        public static bool IsAutoCaptureEnabled()
        {
            return EditorPrefs.GetBool(AUTO_CAPTURE_PREF, true);
        }

        // === 모델 선택 ===

        /// <summary>사용할 Gemini 모델 저장</summary>
        public static void SetModel(string modelName)
        {
            EditorPrefs.SetString(MODEL_PREF, modelName ?? DEFAULT_MODEL);
        }

        /// <summary>현재 선택된 모델명 조회 (기본값: gemini-2.5-flash)</summary>
        public static string GetModelName()
        {
            return EditorPrefs.GetString(MODEL_PREF, DEFAULT_MODEL);
        }

        /// <summary>현재 모델의 AVAILABLE_MODELS 배열 인덱스 반환</summary>
        public static int GetModelIndex()
        {
            string current = GetModelName();
            for (int i = 0; i < AVAILABLE_MODELS.Length; i++)
            {
                if (AVAILABLE_MODELS[i] == current) return i;
            }
            return 0; // 기본값: flash
        }

        // === Gemini API 엔드포인트 ===

        /// <summary>API 호출 URL 생성 (선택된 모델 + API 키 포함)</summary>
        public static string GetAPIEndpoint()
        {
            string apiKey = GetAPIKey();
            string model = GetModelName();
            return $"{GEMINI_API_BASE_URL}{model}:generateContent?key={apiKey}";
        }
    }
}
