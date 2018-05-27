# UnlockFile

UnlockFile allows to unlock a file by shutting down the applications that lock the file.

# How to install UnlockFile?

1. Install .NET Core 2.1: https://www.microsoft.com/net/download/Windows/run
2. Install the UnlockFile:

````
dotnet tool install --global UnlockFile
````

# How to use UnlockFile?

````
UnlockFile "c:\test\file.txt"
````

# Windows Explorer integration

````
UnlockFile RegisterShellMenu --run-as-admin --pause
````
