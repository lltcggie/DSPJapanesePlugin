@echo off

cd /d "%~dp0"
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://docs.google.com/spreadsheets/d/1U9Y3iV7pfYGvlsl_tjvxX5mN0L_YrLlxdnCNnpMAyso/export?format=tsv&gid=1517191263', 'DSPtxt - �ҏW�p.tsv')"
powershell -Command "(New-Object System.Net.WebClient).DownloadFile('https://docs.google.com/spreadsheets/d/1U9Y3iV7pfYGvlsl_tjvxX5mN0L_YrLlxdnCNnpMAyso/export?format=tsv&gid=181811814', 'DSPtxt - Mod�|��_�ҏW�p.tsv')"
rem ���s�R�[�h�����݂��Ă�pandas���o�O��̂Ŏ��O�ɓ��ꂵ�Ă���
powershell -Command "(Get-Content '.\DSPtxt - �ҏW�p.tsv' -Encoding UTF8) | %% { $_ + \"`r`n\" } | Set-Content -LiteralPath '.\DSPtxt - �ҏW�p.tsv' -NoNewline -Encoding UTF8"
python gen.py
pause
