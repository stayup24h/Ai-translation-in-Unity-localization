import csv
import time
import sys
import re
import os

from google.cloud import translate_v3 as translate
from google.api_core.exceptions import GoogleAPIError

# --- 전역 설정 변수 ---
# Unity에서 인자로 받을 예정이므로 초기값은 None
GOOGLE_CLOUD_API_KEY = None
GOOGLE_CLOUD_PROJECT_ID = None

# --- Google Cloud Translation API 클라이언트 초기화 ---
translate_client = None

def initialize_translation_client(api_key=None, project_id=None):
    global translate_client, GOOGLE_CLOUD_PROJECT_ID # 전역 변수임을 명시

    if not project_id:
        print("Python 오류: Google Cloud 프로젝트 ID가 제공되지 않았습니다.")
        sys.exit(1)
    GOOGLE_CLOUD_PROJECT_ID = project_id

    try:
        if api_key:
            translate_client = translate.TranslationServiceClient(client_options={"api_key": api_key})
            print(f"Python: Google Cloud Translation API 클라이언트가 API 키로 초기화되었습니다.")
        else:
            # 서비스 계정 방식 (GOOGLE_APPLICATION_CREDENTIALS 환경 변수 사용)
            translate_client = translate.TranslationServiceClient()
            print(f"Python: Google Cloud Translation API 클라이언트가 서비스 계정으로 초기화되었습니다.")
    except Exception as e:
        print(f"Python 오류: Google Cloud Translation API 클라이언트 초기화 실패: {e}")
        sys.exit(1)

# --- 용어 사전 로드 함수 (기존과 동일) ---
def load_glossary(glossary_path):
    glossary = {}
    if not os.path.exists(glossary_path):
        print(f"Python 경고: 용어 사전 파일 '{glossary_path}'을(를) 찾을 수 없습니다. 용어 사전 없이 진행합니다.")
        return glossary

    print(f"Python: 용어 사전 '{glossary_path}'을(를) 로드 중...")
    try:
        with open(glossary_path, mode='r', newline='', encoding='utf-8') as infile:
            reader = csv.reader(infile)
            for i, row in enumerate(reader):
                if len(row) == 2:
                    korean = row[0].strip()
                    english = row[1].strip()
                    if korean and english:
                        glossary[korean] = english
                else:
                    print(f"Python 경고: 용어 사전 {i+1}번째 행의 형식이 올바르지 않습니다: {row}")

        sorted_glossary_keys = sorted(glossary.keys(), key=len, reverse=True)
        print(f"Python: 용어 사전에서 {len(glossary)}개의 항목을 로드했습니다.")
        return {key: glossary[key] for key in sorted_glossary_keys}

    except Exception as e:
        print(f"Python 오류: 용어 사전 '{glossary_path}' 로드 중 문제 발생: {e}")
        sys.exit(1)


# --- 번역 함수 정의 (Google Cloud Translation API 사용) ---
def translate_texts_in_batch(texts_to_translate, target_language="en", source_language="ko", glossary_data=None):
    if not texts_to_translate:
        return {}
    if translate_client is None:
        print("Python 오류: translate_client가 초기화되지 않았습니다.")
        return {i: f"[번역 실패: 클라이언트 없음]" for i in range(len(texts_to_translate))}
    if GOOGLE_CLOUD_PROJECT_ID is None:
        print("Python 오류: Google Cloud 프로젝트 ID가 설정되지 않았습니다.")
        return {i: f"[번역 실패: 프로젝트 ID 없음]" for i in range(len(texts_to_translate))}

    processed_texts = []
    token_maps = []

    for original_text in texts_to_translate:
        temp_text = original_text
        current_token_map = {}
        token_counter = 0

        if glossary_data:
            for korean_term, english_translation in glossary_data.items():
                # 용어 사전 매칭 시 대소문자 무시 (옵션)
                # re.escape를 사용하여 특수문자 처리 (정규식 패턴으로 사용할 경우)
                # 단순 replace이므로 re.escape는 필요 없지만, 더 복잡한 매칭 시 고려
                if korean_term in temp_text:
                    token = f"__GLOSSARY_TOKEN_{token_counter}__"
                    temp_text = temp_text.replace(korean_term, token)
                    current_token_map[token] = english_translation
                    token_counter += 1

        processed_texts.append(temp_text)
        token_maps.append(current_token_map)

    api_translated_results = {}
    parent = f"projects/{GOOGLE_CLOUD_PROJECT_ID}/locations/global"

    try:
        response = translate_client.translate_text(
            parent=parent,
            contents=processed_texts,
            mime_type="text/plain",
            source_language_code=source_language,
            target_language_code=target_language,
        )

        for i, translation in enumerate(response.translations):
            api_translated_results[i] = translation.translated_text

    except GoogleAPIError as e:
        print(f"Python 오류: Google Cloud Translation API 호출 오류 발생: {e}")
        for i in range(len(texts_to_translate)):
            api_translated_results[i] = f"[번역 실패: API 오류 ({e})]"
    except Exception as e:
        print(f"Python 오류: 예상치 못한 번역 오류 발생: {e}")
        for i in range(len(texts_to_translate)):
            api_translated_results[i] = f"[번역 실패: 알 수 없는 오류 ({e})]"

    finally_translated_results = {}
    for i, api_result in api_translated_results.items():
        restored_text = api_result
        current_token_map = token_maps[i]

        for token, english_term in current_token_map.items():
            restored_text = restored_text.replace(token, english_term)

        finally_translated_results[i] = restored_text

    return finally_translated_results

# --- CSV 번역 처리 함수 (기존과 동일) ---
def smart_translate_korean_to_english_in_csv(input_csv_path, prev_translated_csv_path, output_csv_path, glossary_data, batch_size=50):
    current_data = []
    previous_translated_data_map = {}

    with open(input_csv_path, mode='r', newline='', encoding='utf-8') as infile:
        reader = csv.reader(infile)
        header = next(reader)
        current_data.append(header)
        for row in reader:
            current_data.append(row)

    if os.path.exists(prev_translated_csv_path):
        print(f"Python: 이전 번역 파일 '{prev_translated_csv_path}'을(를) 로드 중...")
        with open(prev_translated_csv_path, mode='r', newline='', encoding='utf-8') as prev_infile:
            prev_reader = csv.reader(prev_infile)
            next(prev_reader)
            for row in prev_reader:
                if len(row) >= 4:
                    key_value = row[0].strip()
                    previous_translated_data_map[key_value] = {
                        'korean': row[3].strip() if len(row) > 3 else '',
                        'english': row[2].strip() if len(row) > 2 else ''
                    }
    else:
        print(f"Python: 이전 번역 파일 '{prev_translated_csv_path}'이(가) 존재하지 않습니다. 전체 파일을 새로 번역합니다.")

    texts_to_batch = []
    batch_map_indices = []

    final_translated_data = [list(row) for row in current_data]

    print("Python: 변경 사항 감지 및 번역 필요 텍스트 수집 중...")
    for i in range(1, len(current_data)):
        row_index = i
        current_row = current_data[row_index]

        if len(current_row) < 4:
            print(f"Python 경고: {row_index+1}번째 행의 열 개수가 부족합니다 (최소 4개 필요). 건너뜀.")
            continue

        key_value = current_row[0].strip()
        current_korean_text = current_row[3].strip()

        prev_data = previous_translated_data_map.get(key_value)

        if not prev_data or \
           prev_data['korean'] != current_korean_text or \
           not prev_data['english']:

            texts_to_batch.append(current_korean_text)
            batch_map_indices.append(row_index)

            if len(texts_to_batch) >= batch_size:
                print(f"Python: [{batch_map_indices[0]+1}~{batch_map_indices[-1]+1}번째 행] {len(texts_to_batch)}개 텍스트 배치 번역 중...")
                translated_batch = translate_texts_in_batch(texts_to_batch, "en", "ko", glossary_data)

                for j, _ in enumerate(texts_to_batch):
                    csv_row_idx = batch_map_indices[j]
                    translated_text = translated_batch.get(j, f"[번역 실패: 배치 인덱스 {j} 없음]")

                    while len(final_translated_data[csv_row_idx]) < 3:
                        final_translated_data[csv_row_idx].append('')
                    final_translated_data[csv_row_idx][2] = translated_text

                texts_to_batch = []
                batch_map_indices = []
                time.sleep(0.5)

        else:
            translated_for_this_row = prev_data['english']
            while len(final_translated_data[row_index]) < 3:
                final_translated_data[row_index].append('')
            final_translated_data[row_index][2] = translated_for_this_row

    if texts_to_batch:
        print(f"Python: [{batch_map_indices[0]+1}~{batch_map_indices[-1]+1}번째 행] 마지막 {len(texts_to_batch)}개 텍스트 배치 번역 중...")
        translated_batch = translate_texts_in_batch(texts_to_batch, "en", "ko", glossary_data)

        for j, _ in enumerate(texts_to_batch):
            csv_row_idx = batch_map_indices[j]
            translated_text = translated_batch.get(j, f"[번역 실패: 마지막 배치 인덱스 {j} 없음]")

            while len(final_translated_data[csv_row_idx]) < 3:
                final_translated_data[csv_row_idx].append('')
            final_translated_data[csv_row_idx][2] = translated_text

        time.sleep(0.5)

    with open(output_csv_path, mode='w', newline='', encoding='utf-8') as outfile:
        writer = csv.writer(outfile)
        writer.writerows(final_translated_data)

    print(f"\nPython: 선택적 번역 및 용어 사전 활용 완료! 결과가 '{output_csv_path}'에 저장되었습니다.")

# --- 스크립트 실행 시작점 (인자 처리) ---
if __name__ == "__main__":
    # 인자 순서: <API_KEY> <PROJECT_ID> <INPUT_CSV_PATH> <OUTPUT_CSV_PATH>
    if len(sys.argv) != 5:
        print("Python 오류: 올바른 사용법: python translation_script.py <api_key> <project_id> <input_csv_path> <output_csv_path>")
        sys.exit(1)

    GOOGLE_CLOUD_API_KEY = sys.argv[1]
    GOOGLE_CLOUD_PROJECT_ID = sys.argv[2]
    input_csv_file = sys.argv[3]
    output_csv_file = sys.argv[4]

    # 클라이언트 초기화
    initialize_translation_client(api_key=GOOGLE_CLOUD_API_KEY, project_id=GOOGLE_CLOUD_PROJECT_ID)

    prev_translated_csv_file = output_csv_file

    glossary_file = os.path.join(os.path.dirname(input_csv_file), 'glossary.csv')
    game_glossary = load_glossary(glossary_file)

    BATCH_SIZE = 50

    print(f"Python: 번역 시작. 입력: '{input_csv_file}', 출력: '{output_csv_file}'")
    smart_translate_korean_to_english_in_csv(input_csv_file, prev_translated_csv_file, output_csv_file, game_glossary, BATCH_SIZE)
    print("Python: 번역 스크립트 실행 완료.")