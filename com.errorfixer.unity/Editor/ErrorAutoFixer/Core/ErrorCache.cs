using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// 에러 분석 결과 캐싱
    /// 동일 에러 메시지가 반복되면 API 호출 없이 이전 결과를 재활용
    /// EditorPrefs에 JSON으로 저장하여 에디터 재시작 후에도 유지
    /// </summary>
    public static class ErrorCache
    {
        // === 상수 ===
        private const int MAX_CACHE_COUNT = 100;
        private const string CACHE_PREF_PREFIX = "ErrorAutoFixer_Cache_";
        private const string CACHE_KEYS_PREF = "ErrorAutoFixer_CacheKeys";

        // === 메모리 캐시 ===
        private static readonly Dictionary<string, AnalysisResult> memoryCache
            = new Dictionary<string, AnalysisResult>();

        private static bool loaded;

        // === 공개 API ===

        /// <summary>캐시된 항목 수</summary>
        public static int Count => EnsureLoaded().Count;

        /// <summary>
        /// 에러 메시지에 대한 캐시된 분석 결과 조회
        /// </summary>
        /// <param name="errorMessage">에러 메시지 원문</param>
        /// <returns>캐시된 결과 (없으면 null)</returns>
        public static AnalysisResult Get(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return null;

            var cache = EnsureLoaded();
            string key = ComputeKey(errorMessage);

            cache.TryGetValue(key, out var result);
            return result;
        }

        /// <summary>
        /// 분석 결과를 캐시에 저장
        /// </summary>
        /// <param name="errorMessage">에러 메시지 원문</param>
        /// <param name="result">저장할 분석 결과</param>
        public static void Put(string errorMessage, AnalysisResult result)
        {
            if (string.IsNullOrEmpty(errorMessage) || result == null) return;

            var cache = EnsureLoaded();
            string key = ComputeKey(errorMessage);

            // 최대 수 초과 시 가장 오래된 항목 제거
            if (!cache.ContainsKey(key) && cache.Count >= MAX_CACHE_COUNT)
            {
                RemoveOldest(cache);
            }

            cache[key] = result;
            SaveEntry(key, result);
        }

        /// <summary>
        /// 캐시 전체 삭제
        /// </summary>
        public static void ClearAll()
        {
            // EditorPrefs에서 캐시 키 목록 읽기
            string keysJson = EditorPrefs.GetString(CACHE_KEYS_PREF, "");
            if (!string.IsNullOrEmpty(keysJson))
            {
                var keys = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(keysJson);
                if (keys != null)
                {
                    foreach (string key in keys)
                    {
                        EditorPrefs.DeleteKey(CACHE_PREF_PREFIX + key);
                    }
                }
            }

            EditorPrefs.DeleteKey(CACHE_KEYS_PREF);
            memoryCache.Clear();
            loaded = true;

            Debug.Log("[ErrorAutoFixer] 캐시가 초기화되었습니다.");
        }

        // === 내부 로직 ===

        /// <summary>
        /// 메모리 캐시 로드 (최초 1회)
        /// </summary>
        private static Dictionary<string, AnalysisResult> EnsureLoaded()
        {
            if (loaded) return memoryCache;
            loaded = true;

            try
            {
                string keysJson = EditorPrefs.GetString(CACHE_KEYS_PREF, "");
                if (string.IsNullOrEmpty(keysJson)) return memoryCache;

                var keys = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(keysJson);
                if (keys == null) return memoryCache;

                foreach (string key in keys)
                {
                    string entryJson = EditorPrefs.GetString(CACHE_PREF_PREFIX + key, "");
                    if (string.IsNullOrEmpty(entryJson)) continue;

                    var result = Newtonsoft.Json.JsonConvert.DeserializeObject<AnalysisResult>(entryJson);
                    if (result != null)
                    {
                        memoryCache[key] = result;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ErrorAutoFixer] 캐시 로드 실패: {ex.Message}");
            }

            return memoryCache;
        }

        /// <summary>
        /// 단일 캐시 엔트리를 EditorPrefs에 저장
        /// </summary>
        private static void SaveEntry(string key, AnalysisResult result)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                EditorPrefs.SetString(CACHE_PREF_PREFIX + key, json);
                SaveKeyList();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ErrorAutoFixer] 캐시 저장 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 캐시 키 목록을 EditorPrefs에 저장
        /// </summary>
        private static void SaveKeyList()
        {
            try
            {
                var keys = new List<string>(memoryCache.Keys);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(keys);
                EditorPrefs.SetString(CACHE_KEYS_PREF, json);
            }
            catch { }
        }

        /// <summary>
        /// 가장 오래된 항목 제거 (Dictionary 순회 기준 첫 번째)
        /// </summary>
        private static void RemoveOldest(Dictionary<string, AnalysisResult> cache)
        {
            string oldestKey = null;
            foreach (var kvp in cache)
            {
                oldestKey = kvp.Key;
                break;
            }

            if (oldestKey != null)
            {
                cache.Remove(oldestKey);
                EditorPrefs.DeleteKey(CACHE_PREF_PREFIX + oldestKey);
            }
        }

        /// <summary>
        /// 에러 메시지의 캐시 키 생성 (해시 기반)
        /// </summary>
        private static string ComputeKey(string errorMessage)
        {
            return errorMessage.GetHashCode().ToString("X8");
        }
    }
}
