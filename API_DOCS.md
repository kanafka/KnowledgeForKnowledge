# API Documentation — KnowledgeForKnowledge

**Base URL:** `http://localhost:5129`  
**Auth:** Bearer JWT в заголовке `Authorization: Bearer <token>`  
**Content-Type:** `application/json` (если не указано иное)

---

## Общие соглашения

### Пагинация

Все списочные ответы возвращают:
```json
{
  "items": [...],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

### Коды ошибок

| HTTP | Когда |
|------|-------|
| `400` | Невалидные данные, нарушение бизнес-правил (`{ "errors": {...} }` или `{ "message": "..." }`) |
| `401` | Не передан / невалидный JWT |
| `403` | Нет прав (чужой ресурс, неверный пароль, неверные учётные данные при входе) |
| `404` | Сущность не найдена |
| `409` | Конфликт данных (дубликат, недопустимое действие) |
| `500` | Внутренняя ошибка |

### Енумы (числовые значения)

```
RequestStatus:           0=Open, 1=Fulfilled, 2=Closed, 3=OnHold
ApplicationStatus:       0=Pending, 1=Accepted, 2=Rejected
DealStatus:              0=Active, 1=CompletedByInitiator, 2=CompletedByPartner, 3=Completed, 4=Cancelled
SkillLevel:              0=Trainee, 1=Junior, 2=Middle, 3=Senior
SkillEpithet:            0=IT, 1=Design, 2=Cooking, 3=Language, 4=Music, 5=Sports, 6=Business, 7=Education, 8=Healthcare, 9=Other
VerificationRequestType: 0=SkillVerify, 1=AccountVerify
VerificationStatus:      0=Pending, 1=Approved, 2=Rejected
NotificationType:        0=NewApplication, 1=ApplicationAccepted, 2=ApplicationRejected,
                         3=DealCreated, 4=DealCompleted, 5=DealCancelled,
                         6=NewReview, 7=VerificationApproved, 8=VerificationRejected
```

---

## AUTH `/api/auth`

### `POST /api/auth/login`

**⚠️ Telegram обязателен для входа.** Если Telegram не привязан — вместо токена возвращается токен привязки и флаг `requiresTelegramLink: true`. Пользователь должен сначала привязать Telegram, затем войти снова.

**Body:**
```json
{ "login": "user@example.com", "password": "secret" }
```

**Ответ 200 — Telegram привязан (2FA):**
```json
{
  "token": "",
  "accountId": "uuid",
  "isAdmin": false,
  "requiresOtp": true,
  "sessionId": "hex32",
  "requiresTelegramLink": false,
  "telegramLinkToken": null
}
```
> Следующий шаг: `POST /api/auth/verify-otp`

**Ответ 200 — Telegram НЕ привязан:**
```json
{
  "token": "",
  "accountId": "uuid",
  "isAdmin": false,
  "requiresOtp": false,
  "sessionId": null,
  "requiresTelegramLink": true,
  "telegramLinkToken": "A3KX9MZP"
}
```
> Следующий шаг: пользователь пишет боту `/start A3KX9MZP`, затем заново `POST /api/auth/login`

**Ошибки:**
- `403` — неверный логин/пароль, аккаунт деактивирован, аккаунт заблокирован (сообщение содержит минуты)

---

### `POST /api/auth/verify-otp`
Второй шаг 2FA — ввод кода из Telegram.

**Body:**
```json
{ "sessionId": "hex32", "code": "123456" }
```

**Ответ 200:**
```json
{ "token": "eyJ..." }
```

**Ошибки:**
- `400` — неверный код (максимум 5 попыток, потом сессия блокируется), сессия истекла (TTL 5 мин)

---

### `POST /api/auth/forgot-password`
Запрос сброса пароля через Telegram. Ответ одинаков независимо от того, существует ли аккаунт.

**Body:**
```json
{ "login": "user@example.com" }
```

**Ответ 200:**
```json
{ "sessionId": "hex32" }
```

> Если аккаунт не найден или Telegram не привязан — `sessionId: ""`. Намеренно, защита от перебора.

---

### `POST /api/auth/reset-password`
Сброс пароля по коду из Telegram.

**Body:**
```json
{ "sessionId": "hex32", "code": "123456", "newPassword": "newSecret123" }
```

**Ответ 204** — пароль изменён.

**Ошибки:**
- `400` — неверный код, сессия истекла (TTL 10 мин)
- `403` — сессия не найдена (недействительный `sessionId`)

---

## ACCOUNTS `/api/accounts`

### `POST /api/accounts`
Регистрация. Публичный.

**Body:**
```json
{
  "login": "user@example.com",
  "password": "secret123",
  "telegramID": null,
  "createTelegramLinkToken": true
}
```

> `telegramID` — опционально, если уже известен chat_id.  
> `createTelegramLinkToken: true` — сервер сразу генерирует токен привязки, удобно для онбординга.

**Ответ 201:**
```json
{
  "accountId": "uuid",
  "telegramLinkToken": "A3KX9MZP"
}
```

> `telegramLinkToken` — `null` если `createTelegramLinkToken: false`

**Ошибки:**
- `400` — логин занят, пароль слишком короткий

---

### `GET /api/accounts/me` 🔒

**Ответ 200:**
```json
{
  "accountID": "uuid",
  "login": "user@example.com",
  "telegramID": "123456789",
  "isAdmin": false,
  "isActive": true,
  "createdAt": "2025-01-01T00:00:00Z"
}
```

---

### `GET /api/accounts/{id}` 🔒
Аккаунт по ID. Формат ответа — тот же что `/me`.

**Ошибки:** `404`

---

### `GET /api/accounts?search=&page=1&pageSize=20` 🔒 Admin only
Список всех аккаунтов с поиском по логину.

**Ответ 200:** Пагинированный список `AccountDto`.

---

### `PUT /api/accounts/{id}` 🔒
Обновить TelegramID вручную. Только свой аккаунт.

**Body:** `{ "telegramID": "123456789" }`

**Ответ 204**.

---

### `PUT /api/accounts/{id}/password` 🔒
Смена пароля. Только свой аккаунт.

**Body:** `{ "currentPassword": "old", "newPassword": "new123" }`

**Ответ 204**.

**Ошибки:** `403` — неверный текущий пароль или чужой аккаунт

---

### `DELETE /api/accounts/{id}` 🔒
Деактивация (soft delete). Пользователь — только свой, Admin — любой.

**Ответ 204**.

---

### `PUT /api/accounts/{id}/activate` 🔒 Admin only
Реактивировать аккаунт.

**Ответ 204**.

---

## USER PROFILES `/api/userprofiles`

### `GET /api/userprofiles/{accountId}`
Публичный. Если профиль не создан — возвращает частичный объект (`hasProfile: false`).  
`contactInfo` виден только самому пользователю и Admin.

**Ответ 200:**
```json
{
  "accountID": "uuid",
  "fullName": "Ivan Ivanov",
  "dateOfBirth": "1995-05-20T00:00:00Z",
  "photoURL": "/uploads/photos/uuid_abc.jpg",
  "contactInfo": "vk.com/ivan",
  "description": "Python developer",
  "isActive": true,
  "lastSeenOnline": "2025-04-01T10:00:00Z",
  "hasProfile": true
}
```

> Для анонима / чужого пользователя `contactInfo` будет `null`.

**Ошибки:** `404` — аккаунт не существует вовсе.

---

### `PUT /api/userprofiles` 🔒
Создать или обновить свой профиль (upsert).

**Body:**
```json
{
  "fullName": "Ivan Ivanov",
  "dateOfBirth": "1995-05-20",
  "photoURL": null,
  "contactInfo": "vk.com/ivan",
  "description": "Python developer"
}
```

**Ответ 204**.

---

### `POST /api/userprofiles/photo` 🔒
Загрузить фото профиля. `multipart/form-data`, поле `photo`.  
Если профиль ещё не создан — создаётся частичный профиль автоматически.

**Ограничения:** JPEG / PNG / WebP, макс 5 МБ.

**Ответ 200:**
```json
{ "photoUrl": "/uploads/photos/uuid_abc.jpg" }
```

**Ошибки:** `400` — файл не выбран, неверный формат, превышен размер.

---

## SKILLS `/api/skills`

### `GET /api/skills?search=&epithet=&page=1&pageSize=20`
Каталог навыков. Публичный.

**Ответ 200:**
```json
{
  "items": [{ "skillID": "uuid", "skillName": "Python", "epithet": 0 }],
  "totalCount": 50, "page": 1, "pageSize": 20, "totalPages": 3
}
```

---

### `POST /api/skills` 🔒 Admin only
**Body:** `{ "skillName": "Rust", "epithet": 0 }`  
**Ответ 201:** `{ "id": "uuid" }`

---

### `DELETE /api/skills/{id}` 🔒 Admin only
**Ответ 204**.

---

## SKILL OFFERS `/api/skilloffers`

### `GET /api/skilloffers`
Список предложений. Публичный. Сортировка: новые первые.

**Query params:**

| Параметр | Тип | Описание |
|---|---|---|
| `skillId` | guid | Фильтр по навыку |
| `accountId` | guid | Фильтр по автору |
| `isActive` | bool | Только активные / неактивные |
| `search` | string | Поиск по заголовку, описанию, навыку |
| `viewerAccountId` | guid | ID смотрящего — для barter-фильтрации |
| `viewerHasSkill` | bool | `true` — только офферы, где смотрящий может помочь (есть нужный навык) |
| `requireBarter` | bool | `true` — только офферы, у автора которых есть запросы на навыки смотрящего |
| `page` | int | По умолчанию 1 |
| `pageSize` | int | По умолчанию 20 |

**Ответ 200:**
```json
{
  "items": [
    {
      "offerID": "uuid",
      "accountID": "uuid",
      "authorName": "Ivan Ivanov",
      "authorPhotoURL": "/uploads/photos/...",
      "skillID": "uuid",
      "skillName": "Python",
      "title": "Обучу Python",
      "details": "Базовый курс",
      "isActive": true
    }
  ],
  "totalCount": 10, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `GET /api/skilloffers/{id}`
Карточка одного предложения. Публичный.

**Ответ 200:** Тот же формат что в списке.  
**Ошибки:** `404`

---

### `POST /api/skilloffers` 🔒
Создать предложение.

**Предварительные условия:**
1. Профиль (`PUT /api/userprofiles`) должен быть заполнен.
2. Навык должен быть добавлен в личный кабинет (`POST /api/userskills`).

**Body:**
```json
{ "skillID": "uuid", "title": "Обучу Python", "details": "Базовый курс" }
```

**Ответ 201:** `{ "id": "uuid" }`

**Ошибки:**
- `400` — профиль не заполнен, навык не добавлен в профиль
- `404` — `skillID` не найден в каталоге

---

### `PUT /api/skilloffers/{id}` 🔒
Обновить своё предложение. Только владелец.

**Body:**
```json
{ "title": "Новый заголовок", "details": "Новое описание", "isActive": false }
```

**Ответ 204**.

---

### `DELETE /api/skilloffers/{id}` 🔒
Удалить предложение.  
Пользователь — только своё. Admin — любое, с уведомлением владельца в Telegram.

**Body (опциональный):**
```json
{ "deletionReason": "Нарушение правил платформы" }
```

> Body необязателен. Если передан `deletionReason` и удаляет Admin — владелец получит сообщение в Telegram.

**Ответ 204**.

**Ошибки:** `403` — не владелец и не Admin.

---

## SKILL REQUESTS `/api/skillrequests`

### `GET /api/skillrequests`
Список запросов на обучение. Публичный. Сортировка: новые первые.

**Query params:**

| Параметр | Тип | Описание |
|---|---|---|
| `skillId` | guid | Фильтр по навыку |
| `accountId` | guid | Фильтр по автору |
| `status` | int | Фильтр по статусу (`RequestStatus`) |
| `search` | string | Поиск по заголовку и описанию |
| `helperAccountId` | guid | ID помощника — для barter-фильтрации |
| `canHelp` | bool | `true` — только запросы, где помощник имеет нужный навык |
| `requireBarter` | bool | `true` — только запросы, у автора которых есть навыки, нужные помощнику |
| `page` | int | По умолчанию 1 |
| `pageSize` | int | По умолчанию 20 |

**Ответ 200:**
```json
{
  "items": [
    {
      "requestID": "uuid",
      "accountID": "uuid",
      "authorName": "Ivan Ivanov",
      "authorPhotoURL": null,
      "skillID": "uuid",
      "skillName": "Python",
      "title": "Ищу репетитора по Python",
      "details": "Нужно с нуля",
      "status": 0
    }
  ],
  "totalCount": 4, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `GET /api/skillrequests/{id}`
Карточка одного запроса. Публичный.

**Ответ 200:** Тот же формат что в списке.  
**Ошибки:** `404`

---

### `POST /api/skillrequests` 🔒
Создать запрос. Требует заполненного профиля.

**Body:**
```json
{ "skillID": "uuid", "title": "Ищу репетитора по Python", "details": "Нужно с нуля" }
```

**Ответ 201:** `{ "id": "uuid" }`

**Ошибки:**
- `400` — профиль не заполнен
- `404` — `skillID` не найден в каталоге

---

### `PUT /api/skillrequests/{id}` 🔒
Изменить статус своего запроса.

**Body:** `{ "status": 2 }`

> Допустимые значения: `0=Open`, `2=Closed`, `3=OnHold`.  
> Статус `1=Fulfilled` устанавливается системой автоматически при принятии заявки.

**Ответ 204**.

---

### `DELETE /api/skillrequests/{id}` 🔒
Удалить запрос.  
Пользователь — только свой. Admin — любой, с уведомлением владельца в Telegram.

**Body (опциональный):**
```json
{ "deletionReason": "Нарушение правил платформы" }
```

**Ответ 204**.

**Ошибки:** `403` — не автор и не Admin.

---

## USER SKILLS `/api/userskills`

### `GET /api/userskills/{accountId}`
Навыки пользователя. Публичный.

**Ответ 200:**
```json
[
  {
    "skillID": "uuid",
    "skillName": "Python",
    "epithet": 0,
    "level": 2,
    "description": "Занимаюсь 3 года, знаю Django и FastAPI",
    "learnedAt": "2021",
    "isVerified": false
  }
]
```

---

### `POST /api/userskills` 🔒
Добавить навык себе.

**Body:**
```json
{
  "skillID": "uuid",
  "level": 2,
  "description": "Занимаюсь 3 года",
  "learnedAt": "2021"
}
```

> `description` и `learnedAt` — опциональные строки. `learnedAt` произвольный текст («2021», «с детства», «НИУ ВШЭ 2022»).

**Ответ 204**.

---

### `DELETE /api/userskills/{skillId}` 🔒
Убрать навык из своего профиля.

**Ответ 204**.

---

## APPLICATIONS `/api/applications`

### `GET /api/applications/incoming?page=1&pageSize=20` 🔒
Входящие заявки (кто откликнулся на мои предложения/запросы, статус `Pending`).

**Ответ 200:**
```json
{
  "items": [
    {
      "applicationID": "uuid",
      "applicantID": "uuid",
      "applicantName": "Petr Petrov",
      "offerID": "uuid",
      "skillRequestID": null,
      "message": "Хочу обменяться",
      "status": 0,
      "createdAt": "2025-04-01T10:00:00Z"
    }
  ],
  "totalCount": 1, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `GET /api/applications/outgoing?page=1&pageSize=20` 🔒
Мои исходящие отклики. Формат аналогичен `incoming`.

---

### `GET /api/applications/processed?status=&page=1&pageSize=20` 🔒
Обработанные заявки. Фильтр `status`: `1=Accepted`, `2=Rejected`.

---

### `POST /api/applications` 🔒
Откликнуться. Указать ровно одно из двух полей.

**Body:**
```json
{ "offerID": "uuid", "skillRequestID": null, "message": "Хочу обменяться" }
```

**Ответ 201:** `{ "id": "uuid" }`

**Ошибки:**
- `400` — оба поля пусты или оба заполнены одновременно
- `409` — уже есть отклик на этот оффер/запрос, нельзя откликнуться на своё предложение

---

### `DELETE /api/applications/{id}` 🔒
Отозвать свой отклик (только пока `Pending`).

**Ответ 204**.

**Ошибки:** `400` — уже обработан, `403` — чужой отклик.

---

### `PUT /api/applications/{id}/respond` 🔒
Принять или отклонить входящую заявку. Только владелец предложения/запроса.

**Body:** `{ "status": 1 }`

> `1=Accepted` → сделка создаётся автоматически, оба получают уведомление.  
> `2=Rejected` → заявитель получает уведомление.

**Ответ 204**.

**Ошибки:** `403` — не владелец, `400` — уже обработана.

---

## DEALS `/api/deals`

### `GET /api/deals?page=1&pageSize=20` 🔒
Мои сделки (все статусы).

**Ответ 200:**
```json
{
  "items": [
    {
      "dealID": "uuid",
      "initiatorID": "uuid",
      "partnerID": "uuid",
      "offerID": "uuid",
      "skillRequestID": null,
      "status": 0,
      "createdAt": "2025-04-01T10:00:00Z",
      "completedAt": null
    }
  ],
  "totalCount": 1, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `GET /api/deals/user/{accountId}?page=1&pageSize=20`
Публичная история завершённых/отменённых сделок. Публичный.

---

### `GET /api/deals/{id}` 🔒
Детали сделки. Только участники.

**Ошибки:** `403` — не участник, `404`.

---

### `PUT /api/deals/{id}/complete` 🔒
Отметить завершённой со своей стороны.

> Один нажал → `CompletedByInitiator` или `CompletedByPartner`.  
> Оба нажали → `Completed`, рассылаются уведомления.

**Ответ 204**.

---

### `PUT /api/deals/{id}/cancel` 🔒
Отменить активную сделку.

**Ответ 204**.

**Ошибки:** `403` — не участник, `400` — уже завершена.

---

## REVIEWS `/api/reviews`

### `GET /api/reviews/{accountId}?page=1&pageSize=20`
Отзывы о пользователе. Публичный.

**Ответ 200:**
```json
{
  "items": [
    {
      "reviewID": "uuid",
      "authorID": "uuid",
      "authorName": "Ivan Ivanov",
      "rating": 5,
      "comment": "Отличный преподаватель",
      "createdAt": "2025-04-01T10:00:00Z"
    }
  ],
  "totalCount": 3, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `POST /api/reviews` 🔒
Оставить отзыв. Только по завершённой сделке, один раз на сделку.

**Body:** `{ "dealID": "uuid", "rating": 5, "comment": "Отлично!" }`

> `rating`: 1–5.

**Ответ 201:** `{ "id": "uuid" }`

**Ошибки:** `400` — сделка не завершена, отзыв уже есть, не участник.

---

## MATCHES `/api/matches`

### `GET /api/matches` 🔒
Умный подбор партнёров.

**Ответ 200:**
```json
[
  {
    "accountID": "uuid",
    "fullName": "Petr Petrov",
    "photoURL": null,
    "matchScore": 2,
    "theyCanTeachMe": [{ "skillID": "uuid", "skillName": "Python" }],
    "iCanTeachThem": [{ "skillID": "uuid", "skillName": "Design" }]
  }
]
```

> `matchScore` — сумма совпадений. Сортировка по убыванию.

---

## EDUCATION `/api/education`

### `GET /api/education/{accountId}` 🔒

**Ответ 200:**
```json
[
  {
    "educationID": "uuid",
    "institutionName": "НИУ ВШЭ",
    "degreeField": "Computer Science",
    "yearCompleted": 2022
  }
]
```

---

### `POST /api/education` 🔒
**Body:**
```json
{ "institutionName": "НИУ ВШЭ", "degreeField": "Computer Science", "yearCompleted": 2022 }
```

> `degreeField` и `yearCompleted` — опциональные.

**Ответ 201:** `{ "id": "uuid" }`

---

### `DELETE /api/education/{id}` 🔒
Удалить свою запись. Только своя (`403` иначе).

**Ответ 204**.

---

## PROOFS `/api/proofs`

### `GET /api/proofs/{accountId}` 🔒

**Ответ 200:**
```json
[
  {
    "proofID": "uuid",
    "skillID": "uuid",
    "skillName": "Python",
    "fileURL": "/uploads/proofs/uuid_abc.pdf",
    "uploadedAt": "2025-04-01T10:00:00Z"
  }
]
```

---

### `POST /api/proofs` 🔒
Загрузить файл. `multipart/form-data`.

**Form fields:**
- `file` — JPEG / PNG / WebP / PDF, макс 10 МБ
- `skillID` _(guid, опционально)_ — привязать к навыку

**Ответ 201:** `{ "id": "uuid", "fileUrl": "/uploads/proofs/..." }`

**Ошибки:** `400` — файл не выбран, неверный формат/размер, достигнут лимит 20 файлов.

---

## VERIFICATION `/api/verification`

### `GET /api/verification?accountId=&status=&page=1&pageSize=20` 🔒
Обычный пользователь видит только свои. Admin — все, с фильтром по `accountId`.

**Ответ 200:**
```json
{
  "items": [
    {
      "verificationRequestID": "uuid",
      "accountID": "uuid",
      "requestType": 0,
      "proofID": "uuid",
      "status": 0,
      "createdAt": "2025-04-01T10:00:00Z"
    }
  ],
  "totalCount": 1, "page": 1, "pageSize": 20, "totalPages": 1
}
```

---

### `POST /api/verification` 🔒
Подать заявку на верификацию.

**Body:** `{ "requestType": 0, "proofID": "uuid" }`

> `proofID` — опционально.

**Ответ 201:** `{ "id": "uuid" }`

---

### `PUT /api/verification/{id}/review` 🔒 Admin only
Рассмотреть заявку.

**Body:**
```json
{ "status": 1, "rejectionReason": null }
```

> `1=Approved`, `2=Rejected`.  
> `rejectionReason` — опциональная причина отказа, передаётся пользователю в уведомлении.

**Ответ 204**.

---

## NOTIFICATIONS `/api/notifications`

### `GET /api/notifications?unreadOnly=false&page=1&pageSize=30` 🔒

**Ответ 200:**
```json
{
  "items": [
    {
      "notificationID": "uuid",
      "type": 0,
      "message": "Новый отклик на ваше предложение",
      "isRead": false,
      "relatedEntityId": "uuid",
      "createdAt": "2025-04-01T10:00:00Z"
    }
  ],
  "totalCount": 5, "page": 1, "pageSize": 30, "totalPages": 1
}
```

> `relatedEntityId` — ID сущности для навигации (заявка, сделка и т.д.).

---

### `PUT /api/notifications/{id}/read` 🔒
Пометить одно уведомление прочитанным.

**Ответ 204**.

**Ошибки:** `404` — уведомление с таким ID не существует.

> Попытка отметить чужое уведомление возвращает `204` молча (без ошибки) — это намеренно для безопасности.

---

### `PUT /api/notifications/read-all` 🔒
Пометить все прочитанными.

**Ответ 204**.

---

## TELEGRAM `/api/telegram`

### `POST /api/telegram/generate-link-token` 🔒
Сгенерировать токен привязки Telegram вручную (если не использовался `createTelegramLinkToken` при регистрации).

**Ответ 200:** `{ "token": "A3KX9MZP" }`

**Ошибки:** `400` — Telegram уже привязан.

---

### `GET /api/telegram/notifications/settings` 🔒
**Ответ 200:** `{ "notificationsEnabled": true }`

---

### `PUT /api/telegram/notifications/settings` 🔒
**Body:** `{ "notificationsEnabled": false }`

**Ответ 204**.

---

### `POST /api/telegram/webhook`
Вебхук Telegram Bot API. Вызывается только серверами Telegram.

---

## Типовые сценарии

### Регистрация и первый вход
```
POST /api/accounts  { createTelegramLinkToken: true }
→ { accountId, telegramLinkToken: "A3KX9MZP" }

Пользователь пишет боту: /start A3KX9MZP
→ Telegram привязан

POST /api/auth/login
→ { requiresOtp: true, sessionId: "..." }

POST /api/auth/verify-otp  { sessionId, code }
→ { token: "eyJ..." }  ← JWT получен
```

### Заполнение профиля
```
PUT  /api/userprofiles                  — имя, описание, контакты
POST /api/userprofiles/photo            — фото
POST /api/userskills  { skillID, level, description, learnedAt }
POST /api/education
POST /api/proofs                        — диплом / сертификат
```

### Бартер навыков
```
POST /api/skilloffers                   — создать предложение
POST /api/skillrequests                 — создать запрос
GET  /api/matches                       — найти подходящих партнёров

POST /api/applications { offerID }      — откликнуться
PUT  /api/applications/{id}/respond { 1 }  — принять → Deal создаётся
PUT  /api/deals/{id}/complete           — оба отмечают завершение
POST /api/reviews { dealID, rating }    — оставить отзыв
```

### Умный поиск с barter-фильтром
```
GET /api/skilloffers?requireBarter=true&viewerAccountId=uuid
    → офферы, авторы которых сами ищут навыки, которые есть у вас

GET /api/skillrequests?canHelp=true&helperAccountId=uuid
    → запросы, в которых вы можете помочь (у вас есть нужный навык)
```

### Модерация (Admin)
```
DELETE /api/skilloffers/{id}  { "deletionReason": "Нарушение" }
    → владелец получает уведомление в Telegram

PUT /api/verification/{id}/review  { "status": 2, "rejectionReason": "Недостаточно документов" }
```
