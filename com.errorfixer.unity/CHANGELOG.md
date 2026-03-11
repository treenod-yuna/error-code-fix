# Changelog

## [0.2.0] - 2026-02-26
### Added
- diff 미리보기 UI (LCS 기반 인라인 diff, 삭제/추가 색상 표시)
- 원클릭 수정 적용 기능 ([수정 적용] 버튼)
- 콘솔 에러 스캔 기능 (기존 에러 자동 감지)
- 컴파일 완료 시 해결된 에러 자동 제거
- 에러 패턴 캐싱 (동일 에러 반복 시 API 호출 생략)
- 설정 UI 고도화 (모델 선택, 캐시 관리)

### Changed
- Track A/B 분기 구현 (수정 가능/진단만 제공)
- ErrorCapture에 LogEntries reflection + CompilationPipeline 연동

### Removed
- BackupManager (Git discard로 대체)

## [0.1.0] - 2026-02-25
### Added
- 에러 로그 자동 캡처 (Application.logMessageReceived)
- 스택 트레이스 파싱 (컴파일 에러 + 런타임 에러)
- Google Gemini API 연동 (gemini-2.5-flash)
- 에러 분석 결과 표시 (EditorWindow)
- API 키 설정 UI
