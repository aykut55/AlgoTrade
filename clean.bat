@echo off
echo Cleaning bin and obj folders...

for /d /r %%d in (bin) do (
    if exist "%%d" (
        echo Deleting %%d
        rmdir /s /q "%%d"
    )
)

for /d /r %%d in (obj) do (
    if exist "%%d" (
        echo Deleting %%d
        rmdir /s /q "%%d"
    )
)

echo Done.
