# Unity Error Auto-Fixer

Unity 에디터에서 발생하는 에러를 **Google Gemini AI**로 자동 분석하고, **원클릭으로 코드를 수정**하는 에디터 확장 패키지입니다.

> 무료 API 티어로 사용 가능 — 별도 과금 없음

---

## 프로젝트 개요

Unity 개발 중 자주 마주치는 `NullReferenceException`, `MissingComponentException`, 컴파일 에러 등을 AI가 자동으로 진단합니다. 수정 가능한 에러는 diff 미리보기와 함께 [수정 적용] 버튼으로 바로 패치할 수 있고, 자동 수정이 어려운 에러는 원인 진단 텍스트를 제공합니다.

### 핵심 동작 흐름

```
에러 발생 → 자동 캡처 → 소스코드 컨텍스트 수집 → Gemini API 분석 → 결과 표시 → 원클릭 수정
```

---

## 실행 방법

👉 **[온라인 튜토리얼 보기](https://treenod-yuna.github.io/error-code-fix/tutorial.html)** — 설치, API 키 발급, 사용법을 안내하는 가이드 페이지입니다.

---

## 기능 설명

### Track A: 자동 수정 (수정 버튼 표시)
스택 트레이스에서 파일/라인을 특정할 수 있고, 단일 파일 수정으로 해결 가능한 에러에 대해 동작합니다.

| 에러 유형 | 수정 패턴 |
|---|---|
| `NullReferenceException` | null 체크 추가 또는 초기화 코드 삽입 |
| `MissingComponentException` | `GetComponent` null 체크 또는 `RequireComponent` 추가 |
| CS 컴파일 에러 (타입 불일치 등) | 해당 라인 직접 수정 |
| `IndexOutOfRangeException` | 범위 검사 추가 |
| `MissingReferenceException` | Destroy된 오브젝트 참조 처리 |

### Track B: 진단만 제공 (텍스트 표시)
여러 파일에 걸친 문제, 설정/프리팹 관련 문제 등 코드 수정만으로 해결이 어려운 에러에 대해 원인과 해결 방향을 안내합니다.

### 기타 기능
- **에러 자동 캡처**: `Application.logMessageReceived` + Unity 콘솔 동기화
- **에러 캐싱**: 동일 에러 반복 시 API 호출 없이 즉시 결과 표시 (최대 100개)
- **모델 선택**: gemini-2.5-flash / flash-lite / pro 중 선택 가능
- **컴파일 연동**: 컴파일 완료 시 해결된 에러 자동 제거
- **API 연결 테스트**: 설정 화면에서 키 유효성을 즉시 확인

---

## 프로젝트 구조

```
error-code-fix/
├── README.md                          # 이 파일
├── CLAUDE.md                          # AI 개발 설계 문서
├── tutorial.html                      # 설치/사용 가이드 웹페이지
│
└── com.errorfixer.unity/              # Unity 패키지 (UPM)
    ├── package.json                   # UPM 패키지 정의 (v0.2.0)
    ├── CHANGELOG.md                   # 변경 이력
    │
    └── Editor/ErrorAutoFixer/
        ├── Core/
        │   ├── ErrorCapture.cs        # 에러 로그 캡처
        │   ├── ErrorParser.cs         # 스택 트레이스 파싱 (파일/라인 추출)
        │   ├── SourceCodeReader.cs    # 에러 관련 C# 소스 파일 읽기
        │   └── ErrorCache.cs          # 에러 패턴 캐싱
        │
        ├── API/
        │   ├── GeminiAPIClient.cs     # Gemini API 호출 (UnityWebRequest)
        │   ├── APIRequestBuilder.cs   # 프롬프트 및 요청 JSON 구성
        │   └── APIResponseParser.cs   # 응답 JSON 파싱 (Track A/B 분기)
        │
        ├── UI/
        │   ├── ErrorFixerWindow.cs    # 메인 EditorWindow (2-패널 레이아웃)
        │   ├── DiffPreviewDrawer.cs   # LCS 기반 인라인 diff 미리보기
        │   └── SettingsWindow.cs      # API 키 입력 및 설정 UI
        │
        ├── Patcher/
        │   └── FilePatcher.cs         # 코드 수정 적용
        │
        └── Settings/
            └── ErrorFixerSettings.cs  # 설정 관리 (EditorPrefs)
```

---

## 기술 스택

| 구분 | 기술 |
|---|---|
| **Unity 패키지** | C# (.NET Standard 2.1), UnityEditor API, UnityWebRequest |
| **AI API** | Google Gemini API (gemini-2.5-flash, 무료 티어) |
| **JSON 처리** | Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) |
| **튜토리얼 페이지** | HTML, CSS, JavaScript (정적 단일 파일) |
| **호환 버전** | Unity 2021.3 LTS 이상 |
| **라이선스** | MIT |

### Gemini API 무료 티어

| 모델 | 분당 요청 | 일일 요청 |
|---|---|---|
| gemini-2.5-flash (기본) | 10회 | ~250회 |
| gemini-2.5-flash-lite | 15회 | ~1,000회 |
| gemini-2.5-pro | 5회 | ~25회 |

---

## AI 활용 내역

### 사용한 AI 도구

| 도구 | 용도 |
|---|---|
| **Claude Code (Anthropic)** | 전체 개발 — 설계, 코드 작성, 디버깅, 리팩토링 |
| **Google Gemini API** | 런타임 기능 — Unity 에러 분석 및 코드 수정안 생성 |

### 개발 방식

이 프로젝트는 **AI 페어 프로그래밍** 방식으로 개발되었습니다.

1. **설계 문서 작성**: CLAUDE.md에 프로젝트 요구사항, 아키텍처, 코딩 규칙을 정의
2. **반복적 구현**: Claude Code에 기능 단위로 구현을 요청하며 점진적으로 개발
3. **3단계 개발**: MVP(에러 캡처 + API 연결) → Phase 2(diff UI + 패치) → Phase 3(캐싱 + 설정 고도화)

### 주요 프롬프트 패턴

- **설계 → 구현**: "CLAUDE.md의 Phase 1 구조대로 MVP를 구현해줘"
- **기능 추가**: "에러 캐싱 기능을 추가해줘. 동일 에러는 API 호출 없이 캐시에서 제공"
- **버그 수정**: "Unity 콘솔과 에러 목록이 동기화되지 않는 문제를 해결해줘"
- **UI 개선**: "diff 미리보기에 LCS 기반 인라인 diff를 적용해줘"
- **문서 작성**: "설치/사용 가이드를 정적 HTML 튜토리얼 페이지로 만들어줘"

### AI가 생성한 범위

- Unity 패키지 전체 C# 코드 (12개 파일)
- 튜토리얼 웹페이지 (tutorial.html)
- 프로젝트 설계 문서 (CLAUDE.md)
- 이 README.md

---

## 라이선스

MIT License
