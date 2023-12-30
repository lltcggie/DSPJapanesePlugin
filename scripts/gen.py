# -*- coding: utf-8 -*-
import pandas as pd
import os
import csv

bom = '\ufeff'

def edit_Header():
    is_find_japanese = False
    lines = None
    english_start_index = None
    with open('Header.txt', encoding='utf-16-le') as f:
        lines = f.readlines()
        for i, line in enumerate(lines):
            if line.startswith('1033,'):
                english_start_index = i
            elif line.startswith('1041,'):
                is_find_japanese = True
    if not is_find_japanese:
        lines.insert(english_start_index + 1, '1041,日本語,jaJP,ja,1033,1\n')
        with open('Header.txt', 'w', encoding='utf-16-le') as file:
            file.writelines(lines)

def japanese_tsv_to_dict(tsv_path, key_name, value_name, skiprows):
    japanese_pd = pd.read_csv(tsv_path, sep='\t', encoding='utf-8', skiprows=skiprows)

    japanese = {}
    for _, row in japanese_pd.iterrows():
        key = row[key_name]
        value = row[value_name]
        if not pd.isnull(value):
            if key in japanese:
                print("{0}: 「{1}」はすでに読み込まれています".format(tsv_path, key))
            japanese[key] = value
        else:
            japanese[key] = None

    return japanese

def gen_1041_txt(txt_name, japanese):
    data = pd.read_csv(os.path.join('1033', txt_name), sep='\t', encoding='utf-16-le', header=None, names=['key', 'none', 'num', 'text'])

    for i in range(len(data)):
        key = data.at[i, 'key']
        if key in japanese:
            value = japanese[key]
            if value is not None:
                value = value.replace("[LF]", "\\r\\n")
                value = value.replace("ダークフォグ", "黒霧")
                data.at[i, 'text'] = value
        else:
            print("Column2が{}の行はありません。".format(key))

    with open(os.path.join('1041', txt_name), 'w', encoding='utf-16-le', newline="\n") as file:
        file.write(bom)
        data.to_csv(file, sep='\t', index=False, na_rep='', header=False, encoding='utf-16-le', quoting=csv.QUOTE_NONE)

def gen_1041_txt_new(txt_name, japanese):
    with open(os.path.join('1041', txt_name), 'w', encoding='utf-16-le', newline="\n") as file:
        file.write(bom)
        for key, value in japanese.items():
            if value is not None:
                value = value.replace("[LF]", "\\r\\n")
                value = value.replace("ダークフォグ", "黒霧")
                file.write('{0}\t\t0\t{1}\n'.format(key, value))

def gen_1041():
    japanese = japanese_tsv_to_dict('DSPtxt - 編集用.tsv', '項目名', '日本語訳（この列を編集）', 3)

    if not os.path.exists('1041'):
        os.mkdir('1041')

    txt_files = [f for f in os.listdir('1033') if f.endswith('.txt')]
    for txt_name in txt_files:
        gen_1041_txt(txt_name, japanese)

    # Mod翻訳は[user].txtに新規で作る
    japanese = japanese_tsv_to_dict('DSPtxt - Mod翻訳_編集用.tsv', '翻訳したい文字列（アイテム名や説明文など）', '日本語訳(この列を編集)', 2)
    gen_1041_txt_new('[user].txt', japanese)

if __name__ == '__main__':
    edit_Header()
    gen_1041()
