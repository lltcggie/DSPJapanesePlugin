@echo off

cd /d "%~dp0"
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://docs.google.com/spreadsheets/d/1U9Y3iV7pfYGvlsl_tjvxX5mN0L_YrLlxdnCNnpMAyso/export?format=tsv&gid=1517191263', 'DSPtxt - 編集用.tsv')"
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://docs.google.com/spreadsheets/d/1U9Y3iV7pfYGvlsl_tjvxX5mN0L_YrLlxdnCNnpMAyso/export?format=tsv&gid=181811814', 'DSPtxt - Mod翻訳_編集用.tsv')"
rem 改行コードが混在しててpandasがバグるので事前に統一しておく
powershell -Command "(Get-Content '.\DSPtxt - 編集用.tsv' -Encoding UTF8) | %% { $_ + \"`r`n\" } | Set-Content -LiteralPath '.\DSPtxt - 編集用.tsv' -NoNewline -Encoding UTF8"
python gen.py
pause
