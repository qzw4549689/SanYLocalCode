@echo off
rem KanbanBoard service launcher (invoked by scheduled task "KanbanBoard")
cd /d C:\Projects\KanbanBoard
C:\Projects\KanbanBoard\python\python.exe app.py --port 8100 >> kanban.log 2>&1
