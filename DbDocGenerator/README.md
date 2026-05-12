 DbDocGenerator

Утилита для генерации технической документации по схеме базы данных MS SQL Server. Поддерживает **HTML** и **Markdown** форматы.



 Быстрый старт

 1. Готовый EXE (рекомендуется)

Файл: DbDocGeneratorUI.exe

Просто запустите — откроется окно:

| Поле                |                         Описание                         |
|---------------------|----------------------------------------------------------|
| **Server**          | Имя сервера SQL (например, `DESKTOP-TR30KF9\SQLEXPRESS`) |
| **Auth**            | Windows Auth или SQL Auth                                |
| **User / Password** | Логин и пароль (для SQL Auth)                            |
| **Database**        | Название базы данных                                     |
| **Format**          | HTML или Markdown                                        |

Нажмите **Generate Documentation** — результат сохранится в папку documentation.

 2. Консольная версия


cd DbDocGenerator
dotnet run -- -d MyDatabase


|     Параметр    | Краткий |                    Описание                    |
|-----------------|---------|------------------------------------------------|
| `--server`      | `-s`    | Сервер (по умолч. localhost)                   |
| `--database`    | `-d`    | База данных (обязательно)                      |
| `--user`        | `-u`    | Пользователь (по умолч. sa)                    |
| `--password`    | `-p`    | Пароль                                         |
| `--trusted`     |         | Windows Auth: true/false (по умолч. true)      |
| `--format`      | `-f`    | html или md (по умолч. html)                   |
| `--output`      | `-o`    | Папка для результата (по умолч. documentation) |

Примеры:


 Windows Auth
dotnet run -- -s localhost -d MyDB

 SQL Auth
dotnet run -- -s localhost -d MyDB -u sa -p pass

 Markdown
dotnet run -- -d MyDB -f md



 Сборка из исходников


 Консольная версия
cd DbDocGenerator
dotnet build

 GUI версия
cd DbDocGeneratorUI
dotnet build


Для публикации одного EXE-файла:


dotnet publish DbDocGeneratorUI\DbDocGeneratorUI.csproj ^
  -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o .\publish




 Структура проекта


проектная работа/
├── DbDocGenerator/          # Консольная версия
│   └── Program.cs           # CLI + вся логика в одном файле
├── DbDocGeneratorUI/        # GUI версия (Windows Forms)
│   ├── MainForm.cs          # Форма
│   ├── SchemaReader.cs      # Чтение схемы БД
│   ├── HtmlGenerator.cs     # Генерация HTML
│   └── MarkdownGenerator.cs # Генерация Markdown
└── проектная работа.sln     # Solution file



 Что генерируется

Документация включает:
- Список таблиц с оглавлением
- Колонки: имя, тип, NULL, значение по умолчанию, описание
- Первичные ключи (PK)
- Внешние ключи (FK) с таблицами-ссылками
- Индексы


 Требования

- Windows (для GUI)
- .NET 8.0 SDK
- MS SQL Server (доступ на чтение системных представлений)