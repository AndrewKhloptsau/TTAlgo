@echo off

set COMPILER=..\..\tools\rsc.exe

if exist BotAgent.cs del BotAgent.cs
%COMPILER%
%COMPILER% -t cs -o BotAgent.cs BotAgent.net

pause