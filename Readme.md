## 1. 개요
이 문서는 Unity 게임 개발 시 다국어 지원을 자동화하는 파이프라인에 대해 설명합니다. 특히 Unity Localization 시스템과 Google Cloud Translation API를 연동하여 CSV 파일 추출, AI 번역, 그리고 번역된 CSV 재가져오기 과정을 자동화하는 워크플로우를 다룹니다. 이 시스템은 변경된 텍스트만 선별적으로 번역하고, 용어 사전을 활용하여 번역 일관성을 유지하며, Unity 에디터 내에서 진행 상황을 프로그레스 바로 표시하여 사용자 편의성을 높입니다.

## 2. 주요 기능
- **Unity String Table CSV 추출** : Unity Localization의 String Table 데이터를 표준 CSV 형식으로 내보냅니다.

- **Google Cloud Translation API 연동**: Gemini API 대신 Google Cloud   Translation API를 사용하여 번역을 수행합니다. 이는 번역에 특화된 고급 기능과 안정적인 인프라를 제공합니다.

- **지능형 번역** :

    - **배치 처리** : API 호출 횟수를 줄이기 위해 여러 문장을 묶어 한 번에 번역을 요청합니다.
    - **변경 사항 추적** : 이전 번역 결과 파일과 현재 원본 파일을 비교하여, 수정되거나 새로 추가된 한국어 텍스트만 선별적으로 번역합니다. 이는 불필요한 API 호출을 줄여 비용을 절감하고 시간을 단축합니다.

    - **용어 사전 활용** : glossary.csv 파일을 사용하여 특정 한국어 단어/구문이 번역해야 할 문장 안에 포함되어 있더라도, 용어 사전에 정의된 영어 번역으로 강제 치환하여 일관성을 유지합니다. (API 호출 전 임시 토큰으로 치환 후, 번역 완료 후 다시 복원하는 방식)

- **Unity 에디터 통합** :

    Unity 에디터 내에 Google Cloud API 키와 프로젝트 ID를 입력할 수 있는 사용자 인터페이스를 제공합니다.

    "translate All" 버튼 하나로 전체 워크플로우를 자동 실행할 수 있습니다.

    작업 진행 상황을 프로그레스 바로 시각적으로 표시하여 사용자에게 현재 상태를 명확히 보여줍니다.

    API 키/프로젝트 ID 관리: Unity 에디터에서 입력받은 API 키와 프로젝트 ID를 파이썬 스크립트로 안전하게 전달하여 사용합니다.

## 3. 시스템 구조
이 시스템은 크게 두 가지 구성 요소로 이루어져 있습니다.

### 3.1. Unity 에디터 스크립트 (C#)
- **UI** : Google Cloud API 키, 프로젝트 ID, 파일 경로 등을 설정할 수 있는 EditorWindow 기반의 사용자 인터페이스를 제공합니다.

- **워크플로우 관리**:
    - **ExportAllStringTablesToCsvAsync()** : Unity Localization String Table을 CSV 파일로 내보냅니다.

    - **RunPythonScript()** : 파이썬 번역 스크립트를 외부 프로세스로 실행합니다.

    - **ImportTranslatedCsvToStringTablesAsync()** : 번역된 CSV 파일을 다시 Unity String Table로 가져옵니다.

    - **자동화 (AutomateAll)** : 위 세 가지 단계를 비동기적으로 순차 실행하여 전체 번역 파이프라인을 자동화합니다.

    - **프로그레스 바** : EditorUtility.DisplayProgressBar를 사용하여 각 단계의 진행 상황을 Unity 에디터 하단에 표시합니다.

    - **인자 전달** : 설정된 API 키, 프로젝트 ID, CSV 파일 경로 등을 파이썬 스크립트로 명령줄 인자를 통해 전달합니다.

### 3.2. 파이썬 번역 스크립트 (translation_script.py)
외부 파이썬 파일로 존재하며, Unity 에디터 스크립트가 호출합니다.

- **API 연동** : google-cloud-translate 라이브러리를 사용하여 Google Cloud Translation API와 통신합니다.

- **CSV 처리** : 입력 CSV 파일을 읽고, 번역을 수행하며, 번역된 결과를 새로운 CSV 파일로 저장합니다.

- **변경 사항 추적** : 입력 CSV와 이전에 번역된 CSV를 비교하여 변경된 한국어 텍스트만 식별합니다.

- **용어 사전 로직** : glossary.csv 파일을 로드하여 사용자 정의 용어를 관리하고, 번역 전 텍스트에서 용어에 해당하는 부분을 임시 토큰으로 치환했다가 번역 완료 후 다시 복원합니다.

- **배치 번역** : 여러 텍스트를 한 번에 API로 전송하여 효율성을 높입니다.

## 4. 설정 및 사용법
### 4.1. Google Cloud Platform 설정

1. **Google Cloud Console 접속**: https://console.cloud.google.com/ 에 접속하여 로그인합니다.

2. **프로젝트 생성/선택** : 새 프로젝트를 생성하거나 기존 프로젝트를 선택합니다.

3. **Cloud Translation API 활성화** :

    - "API 및 서비스" > "라이브러리"로 이동합니다.

    - "Cloud Translation API"를 검색하여 활성화합니다.

4. **API 키 발급** :

    - "API 및 서비스" > "사용자 인증 정보"로 이동합니다.

    - "사용자 인증 정보 만들기" > "API 키"를 클릭하여 API 키를 생성합니다.

    - 발급받은 API 키와 프로젝트 ID를 잘 기록해 둡니다. (프로젝트 ID는 대시보드 상단에서 확인할 수 있습니다.)

### 4.2. 파이썬 환경 설정
1. **파이썬 설치** : 시스템에 Python 3.x 버전이 설치되어 있어야 합니다.

2. **필수 라이브러리 설치** : 명령 프롬프트(또는 터미널)를 열고 다음 명령어를 실행합니다.

```
Bash

pip install google-cloud-translate
```
### 4.3. Unity 프로젝트 설정
용어 사전 파일 생성: translation_script.py 파일과 동일한 디렉토리(또는 ExportedStrings.csv가 생성될 디렉토리)에 glossary.csv 파일을 생성합니다.

glossary.csv 예시:
```
코드 스니펫

아이템,Item
스킬,Skill
퀘스트,Quest
```
(왼쪽 열은 한국어, 오른쪽 열은 해당하는 영어 번역입니다.)

Unity Localization 설정: 프로젝트에 Unity Localization 패키지가 설치되어 있고, String Table Collection이 활성화되어 있는지 확인합니다.

### 4.4. Unity 에디터에서 사용하기
1. **창 열기** : Unity 에디터 상단 메뉴에서 Localization/Automate Translate CSV를 클릭하여 자동화 창을 엽니다.

2. **설정 값 입력** :

    - **Google Cloud API Key** : Google Cloud Platform에서 발급받은 API 키를 입력합니다.

    - **Google Cloud Project ID** : Google Cloud Platform 프로젝트의 ID를 입력합니다.

    - **Export CSV Path** : Localization String Table 데이터가 내보내질 CSV 파일 경로 (예: Assets/LocalizationData/ExportedStrings.csv)

    - **Python Script Path** : translation_script.py 파일의 실제 경로를 입력합니다.

    - **Translated CSV Path** : 번역된 CSV 파일이 저장될 경로 (일반적으로 Export CSV Path와 동일하게 설정하여 이전 번역 추적에 사용)

    - **Python Executable Path** : python 명령어를 실행할 수 있는 파이썬 실행 파일의 경로 (환경 변수에 등록되어 있다면 python 또는 python3 입력)

3. **자동화 실행** : 모든 설정이 완료되면 "Automate All (Export -> Translate -> Import)" 버튼을 클릭합니다.

4. **진행 상황 확인** : Unity 에디터 하단의 프로그레스 바와 콘솔 창을 통해 작업 진행 상황을 확인할 수 있습니다. 작업이 완료되거나 오류 발생 시 프로그레스 바는 사라집니다.

## 5. 중요 고려 사항
**API 할당량 및 비용** : Google Cloud Translation API는 사용량에 따라 비용이 발생합니다. 프로젝트의 할당량 및 비용을 주기적으로 모니터링하여 예상치 못한 지출을 방지하세요.

이 문서는 Unity 게임 개발 시 다국어 지원을 위한 자동 번역 파이프라인을 효과적으로 구축하고 활용하는 데 도움이 될 것입니다.