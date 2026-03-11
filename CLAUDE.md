# CLAUDE.md - Unity Error Auto-Fixer

## 1. 프로젝트 개요
이 프로젝트는 **Unity 에디터용 에러 자동 해결 패키지**입니다.
- Unity 에디터 내에서 발생하는 에러 로그를 자동 캡처
- **Google Gemini API**를 통해 에러 원인 분석 및 C# 코드 수정안 생성
- 자동 수정 가능한 에러는 **[수정] 버튼**으로 원클릭 패치 제공
- 자동 수정이 어려운 에러는 **원인 진단 텍스트**로 안내
- Unity Package Manager로 배포 가능한 패키지 형태
- **무료 API 티어**로 사용 가능 (사용자 비용 부담 없음)

---

## 2. 에러 처리 2-Track 구조

### Track A: 자동 수정 가능 (수정 버튼 표시)
조건:
- 스택 트레이스에 파일 경로/라인 번호가 명확히 존재
- 단일 파일 내에서 수정으로 해결 가능
- Gemini API 응답의 `fixable: true`

대표 에러:
| 에러 | 수정 패턴 |
|---|---|
| `NullReferenceException` | null 체크 추가 또는 초기화 코드 삽입 |
| `MissingComponentException` | `GetComponent` null 체크 또는 `RequireComponent` 추가 |
| CS 컴파일 에러 (타입 불일치 등) | 해당 라인 직접 수정 |
| `IndexOutOfRangeException` | 범위 검사 추가 |
| `MissingReferenceException` | Destroy된 오브젝트 참조 처리 |

### Track B: 진단만 제공 (텍스트 표시)
조건:
- 여러 파일에 걸친 문제
- 설정/프리팹/씬 관련 문제
- 원인이 불확실하거나 코드 수정만으로 해결 불가

대표 에러:
| 에러 | 제공 정보 |
|---|---|
| 스크립트 실행 순서 충돌 | "A.cs와 B.cs의 Awake 순서 문제 → Script Execution Order 설정 필요" |
| 프리팹/씬 참조 누락 | "PlayerController.cs:25 — 인스펙터에서 target 필드가 비어 있음" |
| 에셋 임포트 에러 | "해당 에셋의 Import Settings 확인 필요" |
| 빌드 타겟 관련 에러 | "플랫폼별 조건부 컴파일 확인 필요" |

---

## 3. 핵심 아키텍처

```
[Unity Editor]
    │
    ├─ 1. 에러 로그 캡처 (Application.logMessageReceived)
    │
    ├─ 2. 에러 컨텍스트 수집
    │     ├─ 스택 트레이스에서 파일명/라인 추출
    │     ├─ 해당 C# 파일 소스코드 읽기
    │     └─ 관련 클래스/메서드 컨텍스트 포함
    │
    ├─ 3. Gemini API 호출 (UnityWebRequest → HTTPS POST)
    │     ├─ 엔드포인트: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
    │     ├─ 모델: gemini-2.5-flash (무료 티어, 빠른 응답)
    │     ├─ 시스템 프롬프트: Unity C# 에러 분석 전문가
    │     ├─ 에러 로그 + 소스코드 전달
    │     └─ 구조화된 JSON 응답 수신
    │
    ├─ 4. 결과 표시 (EditorWindow)
    │     ├─ Track A → 원인 설명 + diff 미리보기 + [수정] 버튼
    │     └─ Track B → 원인 설명 + 관련 파일/라인 정보 텍스트
    │
    └─ 5. 자동 패치 (Track A만)
          ├─ 파일 백업 생성
          ├─ File.WriteAllText로 수정 적용
          └─ AssetDatabase.Refresh()로 리컴파일
```

### Gemini API 요청 형식
```json
{
  "contents": [
    {
      "role": "user",
      "parts": [
        {
          "text": "시스템 프롬프트 + 에러 로그 + 소스코드"
        }
      ]
    }
  ],
  "generationConfig": {
    "responseMimeType": "application/json",
    "temperature": 0.1
  }
}
```

### Gemini API 응답 스키마
```json
{
  "fixable": true,
  "confidence": "high | medium | low",
  "diagnosis": "에러 원인 설명 (한국어)",
  "file": "Assets/Scripts/Example.cs",
  "line": 42,
  "patch": {
    "original": "수정 전 코드",
    "fixed": "수정 후 코드"
  }
}
```
- `fixable: true` → Track A (수정 버튼 표시)
- `fixable: false` → Track B (진단 텍스트만 표시, `patch`는 null)

---

## 4. Gemini API 무료 티어 제한

| 모델 | 분당 요청 | 일일 요청 | 비용 |
|---|---|---|---|
| gemini-2.5-pro | 5회 | ~25회 | $0 |
| **gemini-2.5-flash** (채택) | 10회 | ~250회 | $0 |
| gemini-2.5-flash-lite | 15회 | ~1,000회 | $0 |

- 기본 모델: **gemini-2.5-flash** (속도/품질 균형, 일일 250회 충분)
- API 키 발급: https://aistudio.google.com → Google 계정 로그인 → API 키 생성
- 카드 등록 불필요, 기간 제한 없음 (영구 무료)

---

## 5. 언어 및 커뮤니케이션 규칙
- 기본 응답 언어: 한국어
- 설명은 짧고 명확하게 작성
- 불확실한 부분은 추측하지 말고 가정/제약을 명시
- 코드 제공 시 "전체 파일 단위"로 제공
- 수정 요청 시 변경된 파일만 제공하되, 해당 파일은 완성본으로 제공
- 모든 코드에 한글 주석을 작성

---

## 6. 코딩 스타일 가이드

### 기본 원칙
- 모든 코드에 한글 주석 작성
- 단일 책임 원칙 준수, 클래스/메서드는 작게 분리
- 중복 로직은 유틸 메서드로 분리
- 매직 넘버/문자열은 상수로 분리
- 에러 처리는 사용자 경험까지 고려해 명확히 작성

### 네이밍 규칙
- 변수/필드: camelCase
- 메서드/클래스: PascalCase
- 상수: UPPER_SNAKE_CASE
- 파일명: PascalCase.cs
- Editor 스크립트: `Assets/Editor/ErrorAutoFixer/`
- Runtime 스크립트: 없음 (에디터 전용 패키지)

### Unity C# 규칙
- Unity 코딩 컨벤션 준수
- Editor 스크립트는 `#if UNITY_EDITOR` 또는 `Editor/` 폴더에 배치
- public 변수 대신 `[SerializeField] private` 선호
- async/await 사용 시 Unity 메인 스레드 주의 (EditorApplication.delayCall 활용)
- null 체크는 Unity의 null 비교 규칙 고려 (destroyed object)
- UnityWebRequest 사용 시 비동기 처리 및 timeout 설정

---

## 7. 기술 스택

### Unity
- Unity 2021.3 LTS 이상
- C# (.NET Standard 2.1)
- UnityEditor API (EditorWindow, EditorGUILayout 등)
- UnityWebRequest (Gemini API 호출)

### 외부 API
- **Google Gemini API** (GenerateContent API)
- 모델: gemini-2.5-flash (무료 티어)
- 인증: API 키 방식 (URL 파라미터 `?key=API_KEY`)
- JSON 직렬화: Unity 내장 JsonUtility 또는 Newtonsoft.Json

### 개발 환경
- VS Code + Claude Code
- Version Control: Git

### 패키지 배포
- Unity Package Manager (UPM) 호환 구조
- package.json 포함
- OpenUPM 또는 Git URL로 배포

---

## 8. 폴더 구조

### 패키지 구조 (UPM 호환)
```
com.errorfixer.unity/
  package.json                  # UPM 패키지 정의
  README.md                     # 패키지 설명
  CHANGELOG.md                  # 변경 이력

  Editor/
    ErrorAutoFixer/
      Core/
        ErrorCapture.cs          # 에러 로그 캡처 (Application.logMessageReceived)
        ErrorParser.cs           # 스택 트레이스 파싱, 파일/라인 추출
        SourceCodeReader.cs      # 에러 관련 C# 소스 파일 읽기

      API/
        GeminiAPIClient.cs       # Gemini API 호출 (UnityWebRequest)
        APIRequestBuilder.cs     # 프롬프트 및 요청 JSON 구성
        APIResponseParser.cs     # 응답 JSON 파싱 (fixable 분기)

      UI/
        ErrorFixerWindow.cs      # 메인 EditorWindow (에러 목록 + 결과 표시)
        DiffPreviewDrawer.cs     # 수정 전/후 diff 미리보기 UI
        SettingsWindow.cs        # API 키 입력 및 설정 UI

      Patcher/
        FilePatcher.cs           # 파일 백업 + 수정 적용
        BackupManager.cs         # 수정 전 백업 관리 (되돌리기 지원)

      Settings/
        ErrorFixerSettings.cs    # 설정 ScriptableObject (API 키, 옵션 등)
```

---

## 9. 코드 품질 기준

### 일반 품질
- 사용하지 않는 using/변수 금지
- 디버그용 Debug.Log는 커밋 전 제거 (또는 조건부로 변경)
- 미사용 코드 금지
- Unity 경고(Warning) 0개 유지

### 보안 기준
- API 키는 코드에 하드코딩 금지
- API 키 저장: EditorPrefs 사용 (프로젝트별, 로컬 머신에만 저장)
- API 키는 .gitignore로 버전 관리에서 제외
- 소스코드를 API로 전송 시 사용자 동의 절차 포함
- 전송 범위 최소화 (에러 관련 파일/메서드만 전송)

### 성능 기준
- API 호출은 비동기 처리 (에디터 블로킹 금지)
- 에러 캡처는 가볍게 (에디터 성능에 영향 없어야 함)
- 동일 에러 반복 호출 방지 (중복 요청 필터링)
- Gemini 무료 티어 Rate Limit 준수 (분당 10회 이내)

---

## 10. 사용자 경험 설계

### 최초 사용 흐름
1. Unity Package Manager로 패키지 설치
2. 메뉴 `Tools > Error Auto Fixer > Settings`에서 Gemini API 키 입력
3. 에러 발생 시 `Tools > Error Auto Fixer` 창 열기
4. 에러 목록에서 분석할 에러 선택 → [분석] 버튼 클릭
5. Track A → diff 미리보기 확인 후 [수정 적용] 클릭
6. Track B → 진단 텍스트 확인 후 수동 대응

### UI 구성
- **에러 목록 패널**: 최근 에러 리스트 (시간순, 필터 가능)
- **분석 결과 패널**: 선택한 에러의 진단 결과 표시
  - Track A: 원인 + diff + [수정 적용] / [되돌리기] 버튼
  - Track B: 원인 + 관련 파일/라인 정보 텍스트
- **설정 패널**: API 키 입력, 자동 분석 ON/OFF, 언어 설정

---

## 11. 개발 우선순위

### Phase 1 (MVP)
- 에러 로그 캡처 및 파싱
- Gemini API 연결 및 기본 분석
- 단순 EditorWindow에 진단 텍스트 표시

### Phase 2
- Track A/B 분기 구현
- diff 미리보기 UI
- 원클릭 패치 적용 + 백업/되돌리기

### Phase 3
- UPM 패키지 구조 정리 및 배포
- 에러 패턴 캐싱 (동일 에러 반복 시 API 호출 없이 빠른 제안)
- 설정 UI 고도화

---

## 12. 제약사항 및 주의사항

### 기술적 제약
- Gemini API 호출 시 네트워크 연결 필수
- API 응답 지연 가능 (1~5초) → 로딩 UI 필수
- LLM 특성상 수정 제안이 항상 정확하지는 않음 → diff 미리보기 필수
- 대규모 파일은 토큰 제한으로 전체 전송이 어려울 수 있음 (1M 컨텍스트로 대부분 커버)

### Gemini 무료 티어 제약
- 일일 요청 한도: ~250회 (gemini-2.5-flash 기준)
- 분당 요청 한도: 10회
- Rate Limit 초과 시 429 에러 → 재시도 로직 필요

### 배포 관련
- 사용자는 Google AI Studio에서 API 키를 직접 발급받아야 함
- **무료 티어 내에서 사용 가능 (과금 없음)**
- 에디터 전용 (빌드에 포함되지 않음)

---

## 13. Claude에게 추가 지시사항
- Unity 용어는 정확히 사용 (EditorWindow, EditorGUILayout, AssetDatabase 등)
- Unity Editor 스크립트는 에디터 전용 API만 사용
- Gemini API 통신 관련 에러는 사용자에게 명확히 표시
- Gemini API Rate Limit (429) 에러에 대한 재시도 로직 포함
- 모든 C# 코드에 한글 주석 작성 필수
- 패키지 구조는 UPM 규격 준수
- 보안 민감 데이터(API 키)는 절대 코드/로그에 노출 금지