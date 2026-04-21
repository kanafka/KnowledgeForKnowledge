# Схема базы данных — KnowledgeForKnowledge

БД: PostgreSQL. ORM: Entity Framework Core 9.

---

## Диаграмма связей

```
SkillsCatalog ──────────────────────────────────────────┐
     │                                                   │
     │ (1:N)          (1:N)                (1:N)         │
     ▼                ▼                    ▼             │
 UserSkill       SkillOffer           SkillRequest       │
     │                │                    │             │
     └──── Account ───┘────────────────────┘             │
               │                                         │
               │ (1:1)                                   │
               ▼                                         │
          UserProfile                                    │
               │                                         │
     ┌─────────┼──────────┬────────────────┐             │
     ▼         ▼          ▼                ▼             │
 Education   Proof  Notification    Application          │
               │                        │                │
               │                        ▼                │
               │                       Deal              │
               │                        │                │
               └──── VerificationRequest │               │
                                         ▼               │
                                       Review            │
                                                         │
  UserSkill ──────────────────────────────── SkillsCatalog
  SkillOffer ─────────────────────────────── SkillsCatalog
  SkillRequest ───────────────────────────── SkillsCatalog
  Proof ──────────────────────────────────── SkillsCatalog
```

---

## Таблицы

### Accounts
Основная таблица пользователей.

| Колонка | Тип | Nullable | По умолчанию | Описание |
|---|---|---|---|---|
| AccountID | uuid | NO | gen_random_uuid() | PK |
| Login | varchar | NO | — | Уникальный логин (email) |
| PasswordHash | varchar | NO | — | BCrypt хэш пароля |
| TelegramID | varchar | YES | NULL | Chat ID в Telegram (числовой) |
| TelegramLinkToken | varchar | YES | NULL | Временный токен привязки Telegram |
| IsAdmin | bool | NO | false | Флаг администратора |
| IsActive | bool | NO | true | Soft delete |
| NotificationsEnabled | bool | NO | true | Telegram уведомления вкл/выкл |
| FailedLoginAttempts | int | NO | 0 | Счётчик неверных попыток входа |
| LockoutUntil | timestamptz | YES | NULL | Блокировка до этого момента |
| CreatedAt | timestamptz | NO | now() | Дата регистрации |

**Индексы:** уникальный на `Login`

---

### UserProfiles
Профиль пользователя. Связь 1:1 с Accounts (AccountID = PK и FK).

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| AccountID | uuid | NO | PK + FK → Accounts |
| FullName | varchar | NO | Полное имя |
| DateOfBirth | timestamptz | YES | Дата рождения |
| PhotoURL | varchar | YES | Путь к фото `/uploads/photos/...` |
| ContactInfo | varchar | YES | Контакты (только владелец и Admin) |
| Description | varchar | YES | О себе |
| LastSeenOnline | timestamptz | YES | Последний онлайн |
| IsActive | bool | NO | Видимость профиля |

---

### SkillsCatalog
Справочник навыков. Заполняется администратором.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| SkillID | uuid | NO | PK |
| SkillName | varchar | NO | Название навыка |
| Epithet | int | NO | Категория (enum SkillEpithet) |

**Enum SkillEpithet:** `0=IT, 1=Design, 2=Cooking, 3=Language, 4=Music, 5=Sports, 6=Business, 7=Education, 8=Healthcare, 9=Other`

---

### UserSkills
Навыки пользователя. Составной PK.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| AccountID | uuid | NO | PK + FK → Accounts |
| SkillID | uuid | NO | PK + FK → SkillsCatalog |
| SkillLevel | int | NO | Уровень (enum SkillLevel) |
| IsVerified | bool | NO | Подтверждён верификацией |

**Enum SkillLevel:** `0=Trainee, 1=Junior, 2=Middle, 3=Senior`

---

### SkillOffers
Предложения обучения навыку.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| OfferID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts (автор) |
| SkillID | uuid | NO | FK → SkillsCatalog |
| Title | varchar | NO | Заголовок |
| Details | varchar | YES | Подробное описание |
| IsActive | bool | NO | Активно (false при принятой заявке) |
| CreatedAt | timestamptz | NO | Дата создания |

---

### SkillRequests
Запросы на поиск учителя.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| RequestID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts (автор) |
| SkillID | uuid | NO | FK → SkillsCatalog |
| Title | varchar | NO | Заголовок |
| Details | varchar | YES | Подробное описание |
| Status | int | NO | Статус (enum RequestStatus) |
| CreatedAt | timestamptz | NO | Дата создания |

**Enum RequestStatus:** `0=Open, 1=Fulfilled, 2=Closed, 3=OnHold`

---

### Applications
Отклики на предложения и запросы. Ровно одно из `OfferID` / `SkillRequestID` заполнено.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| ApplicationID | uuid | NO | PK |
| ApplicantID | uuid | NO | FK → Accounts (кто откликнулся) |
| OfferID | uuid | YES | FK → SkillOffers |
| SkillRequestID | uuid | YES | FK → SkillRequests |
| Status | int | NO | Статус (enum ApplicationStatus) |
| Message | varchar | YES | Сопроводительное сообщение |
| CreatedAt | timestamptz | NO | Дата отклика |

**Enum ApplicationStatus:** `0=Pending, 1=Accepted, 2=Rejected`

**Частичные уникальные индексы:**
- `(ApplicantID, OfferID)` WHERE `OfferID IS NOT NULL` — один отклик на одно предложение
- `(ApplicantID, SkillRequestID)` WHERE `SkillRequestID IS NOT NULL` — один отклик на один запрос

---

### Deals
Сделки. Создаются автоматически при принятии отклика (`Application.Status = Accepted`).

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| DealID | uuid | NO | PK |
| ApplicationID | uuid | NO | FK → Applications (уникальный) |
| InitiatorID | uuid | NO | FK → Accounts (подавший отклик) |
| PartnerID | uuid | NO | FK → Accounts (владелец оффера/запроса) |
| Status | int | NO | Статус (enum DealStatus) |
| CreatedAt | timestamptz | NO | Дата создания |
| CompletedAt | timestamptz | YES | Дата завершения |

**Enum DealStatus:** `0=Active, 1=CompletedByInitiator, 2=CompletedByPartner, 3=Completed, 4=Cancelled`

**Логика завершения:**
- Один нажал → `CompletedByInitiator` или `CompletedByPartner`
- Оба нажали → `Completed`, записывается `CompletedAt`

---

### Reviews
Отзывы. Один автор — один отзыв на одну сделку.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| ReviewID | uuid | NO | PK |
| DealID | uuid | NO | FK → Deals |
| AuthorID | uuid | NO | FK → Accounts (кто пишет) |
| TargetID | uuid | NO | FK → Accounts (о ком) |
| Rating | int | NO | Оценка 1–5 |
| Comment | varchar | YES | Текст отзыва |
| CreatedAt | timestamptz | NO | Дата |

**Уникальный индекс:** `(DealID, AuthorID)` — один отзыв на сделку от одного автора

---

### Education
Образование пользователя.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| EducationID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts |
| InstitutionName | varchar | NO | Учебное заведение |
| DegreeField | varchar | YES | Специальность / степень |
| YearCompleted | int | YES | Год окончания |

---

### Proofs
Подтверждающие файлы (дипломы, сертификаты).

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| ProofID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts |
| SkillID | uuid | YES | FK → SkillsCatalog (к какому навыку) |
| FileURL | varchar | NO | Путь к файлу `/uploads/proofs/...` |
| IsVerified | bool | NO | Подтверждён администратором |

**Лимит:** максимум 20 файлов на пользователя (проверяется в коде)

---

### VerificationRequests
Заявки на верификацию навыков или аккаунта.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| RequestID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts |
| ProofID | uuid | YES | FK → Proofs |
| RequestType | int | NO | Тип (enum VerificationRequestType) |
| Status | int | NO | Статус (enum VerificationStatus) |
| CreatedAt | timestamptz | NO | Дата подачи |

**Enum VerificationRequestType:** `0=SkillVerify, 1=AccountVerify`  
**Enum VerificationStatus:** `0=Pending, 1=Approved, 2=Rejected`

---

### Notifications
Уведомления внутри приложения.

| Колонка | Тип | Nullable | Описание |
|---|---|---|---|
| NotificationID | uuid | NO | PK |
| AccountID | uuid | NO | FK → Accounts (получатель) |
| Type | int | NO | Тип (enum NotificationType) |
| Message | varchar | NO | Текст уведомления |
| IsRead | bool | NO | Прочитано (false по умолчанию) |
| RelatedEntityId | uuid | YES | ID связанной сущности (Deal, Application…) |
| CreatedAt | timestamptz | NO | Дата |

**Enum NotificationType:**
```
0=NewApplication       — новый отклик на мой оффер/запрос
1=ApplicationAccepted  — мой отклик принят
2=ApplicationRejected  — мой отклик отклонён
3=DealCreated          — создана сделка
4=DealCompleted        — сделка завершена
5=DealCancelled        — сделка отменена
6=NewReview            — получен отзыв
7=VerificationApproved — верификация одобрена
8=VerificationRejected — верификация отклонена
```

---

## Применённые миграции

| Миграция | Что добавлено |
|---|---|
| `20260403204546_InitialCreate` | Все основные таблицы |
| `20260408203701_AddDealsReviewsAndExtensions` | Deals, Reviews, частичные индексы на Applications |
| `20260409133459_AddNotificationsAndAccountSecurity` | Notifications, IsActive/NotificationsEnabled/FailedLoginAttempts/LockoutUntil в Accounts |
| `20260409135715_AddCreatedAtToOffersAndRequests` | CreatedAt в SkillOffers и SkillRequests |
