# KnowledgeForKnowledge

Сервис для обмена знаниями и навыками между пользователями.

## Архитектура

Проект построен на основе чистой архитектуры (Clean Architecture) и состоит из следующих слоёв:

- **Domain** — доменные сущности, перечисления и интерфейсы репозиториев
- **Application** — CQRS команды/запросы (MediatR), FluentValidation валидаторы, pipeline behaviors
- **Infrastructure** — EF Core, PostgreSQL, реализации репозиториев и сервисов (JWT, BCrypt, Telegram)
- **API** — ASP.NET Core 9 контроллеры; все запросы идут через MediatR
- **Tests** — юнит-тесты (xUnit) и E2E-интеграционные тесты (WebApplicationFactory + InMemory EF)

## Технологии

- .NET 9.0 / ASP.NET Core 9
- MediatR 12 (CQRS)
- FluentValidation
- Entity Framework Core 9 + PostgreSQL (Npgsql)
- BCrypt.Net (хэширование паролей)
- JWT Bearer (аутентификация)
- Telegram Bot API (2FA, уведомления)
- xUnit + FluentAssertions (тесты)

## Структура базы данных

| Таблица | Назначение |
|---|---|
| Accounts | Аккаунты пользователей |
| UserProfiles | Профили (имя, фото, описание) |
| SkillsCatalog | Каталог навыков (только Admin) |
| UserSkills | Навыки пользователя |
| Education | Образование |
| Proofs | Подтверждающие файлы (дипломы) |
| SkillOffers | Предложения обучить навыку |
| SkillRequests | Запросы на поиск учителя |
| Applications | Отклики на оферы/запросы |
| Deals | Сделки (создаются при принятии отклика) |
| Reviews | Отзывы после завершённых сделок |
| VerificationRequests | Заявки на верификацию |
| Notifications | Внутренние уведомления |

Подробнее — в [`DB_SCHEMA.md`](DB_SCHEMA.md).

## Запуск

### Через Docker Compose

```bash
docker-compose up -d
```

### Локально

1. Убедитесь, что PostgreSQL запущен.
2. Обновите строку подключения в `API/appsettings.json`.
3. Примените миграции:
   ```bash
   dotnet ef database update --project Infrastructure --startup-project API
   ```
4. Запустите API:
   ```bash
   dotnet run --project API
   ```
   API будет доступен по адресам `https://localhost:5001` / `http://localhost:5000`.

## Миграции EF Core

```bash
# Создать новую миграцию
dotnet ef migrations add <Name> --project Infrastructure --startup-project API

# Применить миграции
dotnet ef database update --project Infrastructure --startup-project API

# Откатить последнюю
dotnet ef migrations remove --project Infrastructure --startup-project API
```

## Тесты

### Запуск

```bash
dotnet test Tests/Tests.csproj
```

### Структура

```
Tests/
├── E2e/
│   ├── Helpers/
│   │   ├── WebAppFactory.cs      # WebApplicationFactory<Program> с InMemory EF
│   │   └── E2eTestBase.cs        # Базовый класс: RegisterAndLoginAsync, SeedSkillAsync, …
│   └── Controllers/
│       ├── AccountsControllerTests.cs
│       ├── AuthControllerTests.cs
│       ├── ApplicationsControllerTests.cs
│       ├── DealsControllerTests.cs
│       └── …
├── InfraServices/                 # Юнит-тесты инфраструктурных сервисов
├── Notifications/                 # Юнит-тесты обработчиков уведомлений
└── Tests.csproj
```

### Ключевые особенности тестовой инфраструктуры

- **Изоляция**: каждый тестовый класс получает свой экземпляр `WebAppFactory` через `IClassFixture<WebAppFactory>` с отдельной InMemory БД — тесты не влияют друг на друга.
- **Environment "Testing"**: `AddInfrastructure` пропускает регистрацию Npgsql; фабрика подключает `UseInMemoryDatabase` вместо PostgreSQL.
- **JWT**: токены генерируются напрямую через `IJwtService` из DI-контейнера, минуя двухшаговый Telegram-вход.
- **NoOp Telegram**: `ITelegramService` заменён заглушкой — никаких реальных HTTP-запросов в тестах.
- **Pipeline behaviors**: `ValidationBehavior` и `ExceptionHandlingBehavior` используют ограничение `where TRequest : notnull` (совместимо с MediatR 12, где void-команды больше не наследуют `IRequest<Unit>`).

## Глобальная обработка исключений

| Исключение | HTTP-код |
|---|---|
| `FluentValidation.ValidationException` | 400 Bad Request |
| `NotFoundException` | 404 Not Found |
| `UnauthorizedAccessException` | 403 Forbidden |
| `InvalidOperationException` | 409 Conflict |
| `DbUpdateException` (уникальное ограничение) | 409 Conflict |
| всё остальное | 500 Internal Server Error |

## API

Полная документация — в [`API_DOCS.md`](API_DOCS.md).

## Настройка Git

Проект уже сконфигурирован для работы с Git:
- `.gitignore` — игнорирует артефакты сборки, IDE, временные файлы
- `.gitattributes` — нормализует окончания строк
- `appsettings.Development.json` и прочие конфиденциальные файлы в игнор-листе
