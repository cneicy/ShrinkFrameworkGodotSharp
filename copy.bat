@echo off
echo ���ڸ���ģ���ļ�...

REM ����Դ�ļ�·��
set SOURCE_MIXIN=D:\Projects\ShrinkFrameworkGodotSharp\TheMixinMod\.godot\mono\temp\bin\Debug\TheMixinMod.dll
set SOURCE_BEMIXINED=D:\Projects\ShrinkFrameworkGodotSharp\TheModBeMixined\.godot\mono\temp\bin\Debug\TheModBeMixined.dll

REM ����Ŀ���ļ���·��
set TARGET_MIXIN=C:\Users\ASUS\AppData\Roaming\Godot\app_userdata\ShrinkFrameworkGodotSharp\mods\TheMixinMod\
set TARGET_BEMIXINED=C:\Users\ASUS\AppData\Roaming\Godot\app_userdata\ShrinkFrameworkGodotSharp\mods\TheModBeMixined\

REM ����Ŀ���ļ��У���������ڣ�
if not exist "%TARGET_MIXIN%" mkdir "%TARGET_MIXIN%"
if not exist "%TARGET_BEMIXINED%" mkdir "%TARGET_BEMIXINED%"

REM ���Դ�ļ��Ƿ���ڲ�����
if exist "%SOURCE_MIXIN%" (
    copy "%SOURCE_MIXIN%" "%TARGET_MIXIN%"
    echo ? TheMixinMod.dll ���Ƴɹ�
) else (
    echo ? Դ�ļ�������: %SOURCE_MIXIN%
)

if exist "%SOURCE_BEMIXINED%" (
    copy "%SOURCE_BEMIXINED%" "%TARGET_BEMIXINED%"
    echo ? TheModBeMixined.dll ���Ƴɹ�
) else (
    echo ? Դ�ļ�������: %SOURCE_BEMIXINED%
)

echo.
echo ���Ʋ�����ɣ�
pause