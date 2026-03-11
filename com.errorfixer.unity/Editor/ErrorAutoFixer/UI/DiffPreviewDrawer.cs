using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ErrorAutoFixer
{
    /// <summary>
    /// 수정 전/후 코드 diff 미리보기 UI
    /// 인라인 diff 형식으로 삭제(빨강)/추가(초록) 라인을 시각적으로 표시
    /// </summary>
    public static class DiffPreviewDrawer
    {
        // === 색상 상수 ===
        private static readonly Color REMOVED_BG = new Color(1f, 0.3f, 0.3f, 0.15f);  // 삭제 라인 배경 (빨강)
        private static readonly Color ADDED_BG = new Color(0.3f, 0.85f, 0.3f, 0.15f);  // 추가 라인 배경 (초록)
        private static readonly Color CONTEXT_BG = new Color(0.5f, 0.5f, 0.5f, 0.05f); // 변경 없는 라인 배경
        private static readonly Color LINE_NUM_COLOR = new Color(0.5f, 0.5f, 0.5f, 0.7f); // 라인 번호 색상

        // === 스타일 캐시 ===
        private static GUIStyle codeStyle;
        private static GUIStyle lineNumStyle;
        private static GUIStyle headerStyle;
        private static bool stylesInitialized;

        /// <summary>
        /// diff 라인 종류
        /// </summary>
        private enum DiffLineType
        {
            Context,  // 변경 없음
            Removed,  // 삭제됨 (수정 전에만 존재)
            Added     // 추가됨 (수정 후에만 존재)
        }

        /// <summary>
        /// diff 한 줄의 정보
        /// </summary>
        private struct DiffLine
        {
            public DiffLineType type;
            public string text;
            public int lineNumber; // 원본 기준 라인 번호 (-1이면 표시 안 함)
        }

        /// <summary>
        /// 패치 정보를 기반으로 diff 미리보기 UI를 그리기
        /// </summary>
        /// <param name="patch">수정 전/후 코드를 담은 패치 정보</param>
        /// <param name="startLine">시작 라인 번호 (1-based, 0이면 라인 번호 미표시)</param>
        public static void DrawDiffPreview(PatchInfo patch, int startLine = 0)
        {
            if (patch == null || string.IsNullOrEmpty(patch.original) || string.IsNullOrEmpty(patch.fixedCode))
            {
                EditorGUILayout.HelpBox("비교할 코드 정보가 없습니다.", MessageType.Warning);
                return;
            }

            InitStyles();

            // diff 계산
            var diffLines = ComputeDiff(patch.original, patch.fixedCode, startLine);

            // 헤더
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("수정 미리보기", headerStyle);
            GUILayout.FlexibleSpace();

            // 변경 통계 표시
            int removedCount = 0, addedCount = 0;
            foreach (var line in diffLines)
            {
                if (line.type == DiffLineType.Removed) removedCount++;
                else if (line.type == DiffLineType.Added) addedCount++;
            }
            var statsStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            EditorGUILayout.LabelField(
                $"<color=#FF6666>-{removedCount}</color>  <color=#66CC66>+{addedCount}</color>",
                statsStyle, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // diff 라인 렌더링
            EditorGUILayout.BeginVertical("box");

            foreach (var line in diffLines)
            {
                DrawDiffLine(line);
            }

            EditorGUILayout.EndVertical();
        }

        // === diff 계산 ===

        /// <summary>
        /// 수정 전/후 코드를 비교하여 diff 라인 목록 생성 (단순 LCS 기반)
        /// </summary>
        private static List<DiffLine> ComputeDiff(string original, string fixedCode, int startLine)
        {
            var result = new List<DiffLine>();

            string[] origLines = NormalizeAndSplit(original);
            string[] fixedLines = NormalizeAndSplit(fixedCode);

            // LCS (Longest Common Subsequence) 기반 diff
            var lcs = ComputeLCS(origLines, fixedLines);

            int oi = 0, fi = 0, li = 0;
            int lineNum = startLine > 0 ? startLine : 1;

            while (li < lcs.Count)
            {
                var (origIdx, fixedIdx) = lcs[li];

                // LCS 항목 이전의 origLines → 삭제된 라인
                while (oi < origIdx)
                {
                    result.Add(new DiffLine
                    {
                        type = DiffLineType.Removed,
                        text = origLines[oi],
                        lineNumber = startLine > 0 ? lineNum : -1
                    });
                    oi++;
                    lineNum++;
                }

                // LCS 항목 이전의 fixedLines → 추가된 라인
                while (fi < fixedIdx)
                {
                    result.Add(new DiffLine
                    {
                        type = DiffLineType.Added,
                        text = fixedLines[fi],
                        lineNumber = -1
                    });
                    fi++;
                }

                // LCS 일치 항목 → 컨텍스트 라인
                result.Add(new DiffLine
                {
                    type = DiffLineType.Context,
                    text = origLines[origIdx],
                    lineNumber = startLine > 0 ? lineNum : -1
                });

                oi = origIdx + 1;
                fi = fixedIdx + 1;
                li++;
                lineNum++;
            }

            // 남은 삭제 라인
            while (oi < origLines.Length)
            {
                result.Add(new DiffLine
                {
                    type = DiffLineType.Removed,
                    text = origLines[oi],
                    lineNumber = startLine > 0 ? lineNum : -1
                });
                oi++;
                lineNum++;
            }

            // 남은 추가 라인
            while (fi < fixedLines.Length)
            {
                result.Add(new DiffLine
                {
                    type = DiffLineType.Added,
                    text = fixedLines[fi],
                    lineNumber = -1
                });
                fi++;
            }

            return result;
        }

        /// <summary>
        /// LCS (Longest Common Subsequence) 알고리즘
        /// 두 문자열 배열의 공통 부분 수열 인덱스 쌍 반환
        /// </summary>
        private static List<(int origIdx, int fixedIdx)> ComputeLCS(string[] a, string[] b)
        {
            int m = a.Length;
            int n = b.Length;

            // DP 테이블 구성
            int[,] dp = new int[m + 1, n + 1];
            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (a[i - 1].TrimEnd() == b[j - 1].TrimEnd())
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Mathf.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            // 역추적으로 LCS 인덱스 쌍 추출
            var result = new List<(int, int)>();
            int ci = m, cj = n;
            while (ci > 0 && cj > 0)
            {
                if (a[ci - 1].TrimEnd() == b[cj - 1].TrimEnd())
                {
                    result.Add((ci - 1, cj - 1));
                    ci--;
                    cj--;
                }
                else if (dp[ci - 1, cj] >= dp[ci, cj - 1])
                {
                    ci--;
                }
                else
                {
                    cj--;
                }
            }

            result.Reverse();
            return result;
        }

        // === UI 렌더링 ===

        /// <summary>
        /// diff 한 줄을 렌더링
        /// </summary>
        private static void DrawDiffLine(DiffLine line)
        {
            // 배경 색상 선택
            Color bgColor;
            string prefix;
            switch (line.type)
            {
                case DiffLineType.Removed:
                    bgColor = REMOVED_BG;
                    prefix = "- ";
                    break;
                case DiffLineType.Added:
                    bgColor = ADDED_BG;
                    prefix = "+ ";
                    break;
                default:
                    bgColor = CONTEXT_BG;
                    prefix = "  ";
                    break;
            }

            // 라인 렌더링
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(16));

            // 배경 그리기
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, bgColor);
            }

            // 라인 번호
            if (line.lineNumber > 0)
            {
                EditorGUILayout.LabelField(line.lineNumber.ToString(), lineNumStyle, GUILayout.Width(35));
            }
            else
            {
                // 추가된 라인은 라인 번호 대신 빈 공간
                GUILayout.Space(38);
            }

            // prefix (- / + / 공백) + 코드
            string displayText = prefix + line.text;
            EditorGUILayout.LabelField(displayText, codeStyle);

            EditorGUILayout.EndHorizontal();
        }

        // === 스타일 초기화 ===

        private static void InitStyles()
        {
            if (stylesInitialized) return;

            codeStyle = new GUIStyle(EditorStyles.label)
            {
                font = GetMonospaceFont(),
                fontSize = 11,
                wordWrap = false,
                richText = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };

            lineNumStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                font = GetMonospaceFont(),
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = LINE_NUM_COLOR },
                padding = new RectOffset(0, 4, 0, 0)
            };

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11
            };

            stylesInitialized = true;
        }

        /// <summary>
        /// 모노스페이스 폰트 로드 (Consolas 또는 기본 폰트)
        /// </summary>
        private static Font GetMonospaceFont()
        {
            // Unity 에디터에서 사용 가능한 모노스페이스 폰트 시도
            var font = Font.CreateDynamicFontFromOSFont("Consolas", 11);
            if (font != null) return font;

            font = Font.CreateDynamicFontFromOSFont("Courier New", 11);
            if (font != null) return font;

            // 모노스페이스 폰트가 없으면 기본 폰트 사용
            return null;
        }

        // === 유틸리티 ===

        /// <summary>
        /// 줄바꿈 정규화 후 라인 분리
        /// </summary>
        private static string[] NormalizeAndSplit(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        }
    }
}
