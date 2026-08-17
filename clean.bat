@echo off
set "ROOT=D:\Aykut\Projects\AlgoTrade"

echo Cleaning bin and obj folders...

for /d /r "%ROOT%" %%d in (bin) do (
    if exist "%%d" (
        echo Deleting %%d
        rmdir /s /q "%%d"
    )
)

for /d /r "%ROOT%" %%d in (obj) do (
    if exist "%%d" (
        echo Deleting %%d
        rmdir /s /q "%%d"
    )
)

echo Cleaning __pycache__ folders...

for /d /r "%ROOT%" %%d in (__pycache__) do (
    if exist "%%d" (
        echo Deleting %%d
        rmdir /s /q "%%d"
    )
)

echo Done.
