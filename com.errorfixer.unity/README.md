# Error Auto Fixer

Unity 에디터용 에러 자동 분석 및 수정 패키지입니다.
Google Gemini API(무료)를 활용하여 에러 원인을 분석하고 코드 수정안을 제공합니다.

## 주요 기능
- **에러 자동 캡처** - 컴파일/런타임 에러를 실시간 감지
- **AI 에러 분석** - Gemini API로 에러 원인 진단 및 해결 방법 제시
- **원클릭 수정** - 수정 가능한 에러는 diff 미리보기 후 [수정 적용] 클릭으로 패치
- **진단 안내** - 자동 수정이 어려운 에러는 원인과 해결 방향 텍스트 제공
- **에러 캐싱** - 동일 에러 반복 시 API 호출 없이 즉시 이전 결과 표시

## 설치

### Git URL (권장)
Unity Package Manager > Add package from git URL:
```
https://github.com/treenod-yuna/error-code-fix.git?path=com.errorfixer.unity
```

### 로컬 경로
`Packages/manifest.json`에 추가:
```json
"com.errorfixer.unity": "file:../경로/com.errorfixer.unity"
```

## 사용법
1. **API 키 설정**: `Tools > Error Auto Fixer > Settings` 에서 Gemini API 키 입력
2. **에러 감지**: 에러 발생 시 `Tools > Error Auto Fixer > Open Window` 열기
3. **분석**: 에러 선택 후 [분석] 버튼 클릭
4. **수정 적용**: Track A(수정 가능) 에러는 diff 확인 후 [수정 적용] 클릭

## 요구사항
- Unity 2021.3 LTS 이상
- Google Gemini API 키 (무료 발급: https://aistudio.google.com)
- Newtonsoft.Json (자동 설치)

## API 키 발급 (무료)
1. https://aistudio.google.com 접속
2. Google 계정 로그인
3. API 키 생성 (카드 등록 불필요, 영구 무료)

## 라이선스
MIT License
