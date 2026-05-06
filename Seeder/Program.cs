using Npgsql;

const string connStr = "Host=localhost;Port=5432;Database=KnowledgeForKnowledge;Username=postgres;Password=postgres";

// --- SETADMIN MODE ---
if (args.Length > 1 && args[0] == "setadmin")
{
    await using var c = new NpgsqlConnection(connStr);
    await c.OpenAsync();
    var cmd = c.CreateCommand();
    cmd.CommandText = @"UPDATE ""Accounts"" SET ""IsAdmin""=true WHERE ""Login""=@l RETURNING ""Login""";
    cmd.Parameters.AddWithValue("l", args[1]);
    var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine(await r.ReadAsync() ? $"Готово: {r["Login"]} теперь Admin" : "Аккаунт не найден.");
    return;
}

// --- APPLY MODE: создать отклики и отправить TG-уведомление ---
if (args.Length > 0 && args[0] == "apply")
{
    const string botToken = "8600408115:AAFsF2M8wjWuogC9f2E3oPlaANV2og94p9U";
    var http = new System.Net.Http.HttpClient();

    await using var c = new NpgsqlConnection(connStr);
    await c.OpenAsync();

    // Данные из БД (из query выше):
    // art.babarov — владелец Docker-оффера и PM-запроса
    var artAccountId   = Guid.Parse("caeb4714-1b49-4222-a531-0e8c6ca686b1");
    var artTelegramId  = "889988062";
    var dockerOfferId  = Guid.Parse("3f60c6dc-cf13-4ed4-8c9b-ac3a8ecc200d");
    var pmRequestId    = Guid.Parse("2c051a62-0567-41b0-bd34-6b768c275f56");

    // Кто откликается:
    // sergey@k4k.ru — DevOps/Docker эксперт
    var sergeyId = Guid.Parse("9eb5cdcf-790a-44b7-8010-35d09a3ca498");
    // roman@k4k.ru  — Project Management эксперт
    var romanId  = Guid.Parse("5711f466-d03e-4928-8a13-fd20d4d76238");

    async Task InsertApplication(Guid applicantId, Guid? offerId, Guid? requestId, string message)
    {
        var appId = Guid.NewGuid();
        var cmd2 = c.CreateCommand();
        cmd2.CommandText = @"
            INSERT INTO ""Applications"" (""ApplicationID"", ""ApplicantID"", ""OfferID"", ""SkillRequestID"", ""Status"", ""Message"", ""CreatedAt"")
            VALUES (@id, @ap, @of, @rq, 0, @msg, @now)";
        cmd2.Parameters.AddWithValue("id",  appId);
        cmd2.Parameters.AddWithValue("ap",  applicantId);
        cmd2.Parameters.AddWithValue("of",  offerId.HasValue ? offerId.Value : DBNull.Value);
        cmd2.Parameters.AddWithValue("rq",  requestId.HasValue ? requestId.Value : DBNull.Value);
        cmd2.Parameters.AddWithValue("msg", message);
        cmd2.Parameters.AddWithValue("now", DateTime.UtcNow);
        await cmd2.ExecuteNonQueryAsync();
        Console.WriteLine($"  Отклик создан: {appId}");

        // Отправляем TG-уведомление владельцу (art.babarov)
        var text = offerId.HasValue
            ? $"На ваше объявление «Помогу с docker» поступил новый отклик.%0AID отклика: {appId}"
            : $"На ваш запрос «Нужна помощь с менеджментом проектов» поступил новый отклик.%0AID отклика: {appId}";
        var url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={artTelegramId}&text={text}";
        var resp = await http.GetAsync(url);
        Console.WriteLine($"  TG: {(resp.IsSuccessStatusCode ? "отправлено ✓" : "ошибка: " + await resp.Content.ReadAsStringAsync())}");
    }

    Console.WriteLine("1. Сергей Попов откликается на Docker-оффер...");
    await InsertApplication(sergeyId, dockerOfferId, null,
        "Привет! Я DevOps-инженер с 7 годами опыта. Работаю с Docker ежедневно, помогу разобраться с контейнеризацией.");

    Console.WriteLine("2. Роман Кузнецов откликается на запрос Project Management...");
    await InsertApplication(romanId, null, pmRequestId,
        "Привет! Я PM с 10 годами опыта в IT, сертифицирован PMP. Готов помочь освоить Agile и Scrum.");

    Console.WriteLine("\nГотово! Проверь Telegram.");
    return;
}

// --- QUERY MODE ---
if (args.Length > 0 && args[0] == "query")
{
    await using var c = new NpgsqlConnection(connStr);
    await c.OpenAsync();

    Console.WriteLine("=== АККАУНТЫ ===");
    var cmd = c.CreateCommand();
    cmd.CommandText = @"SELECT a.""AccountID"", a.""Login"", a.""TelegramID"", p.""FullName"" FROM ""Accounts"" a LEFT JOIN ""UserProfiles"" p ON a.""AccountID""=p.""AccountID"" ORDER BY a.""CreatedAt"" DESC";
    var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) Console.WriteLine($"  {r["Login"]} | TG={r["TelegramID"]} | {r["FullName"]} | {r["AccountID"]}");
    await r.CloseAsync();

    Console.WriteLine("\n=== SKILL OFFERS ===");
    cmd = c.CreateCommand();
    cmd.CommandText = @"SELECT o.""OfferID"", o.""Title"", a.""Login"", s.""SkillName"" FROM ""SkillOffers"" o JOIN ""Accounts"" a ON o.""AccountID""=a.""AccountID"" JOIN ""SkillsCatalog"" s ON o.""SkillID""=s.""SkillID""";
    r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) Console.WriteLine($"  [{r["Login"]}] [{r["SkillName"]}] {r["Title"]} | {r["OfferID"]}");
    await r.CloseAsync();

    Console.WriteLine("\n=== SKILL REQUESTS ===");
    cmd = c.CreateCommand();
    cmd.CommandText = @"SELECT q.""RequestID"", q.""Title"", a.""Login"", s.""SkillName"" FROM ""SkillRequests"" q JOIN ""Accounts"" a ON q.""AccountID""=a.""AccountID"" JOIN ""SkillsCatalog"" s ON q.""SkillID""=s.""SkillID""";
    r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync()) Console.WriteLine($"  [{r["Login"]}] [{r["SkillName"]}] {r["Title"]} | {r["RequestID"]}");
    await r.CloseAsync();

    return;
}

// --- DELETE MODE ---
if (args.Length > 0 && args[0] == "delete")
{
    var login = args.Length > 1 ? args[1] : throw new Exception("Укажи email: dotnet run delete email@example.com");
    await using var c = new NpgsqlConnection(connStr);
    await c.OpenAsync();
    await using var cmd = c.CreateCommand();
    cmd.CommandText = @"DELETE FROM ""Accounts"" WHERE ""Login"" = @l RETURNING ""AccountID"", ""Login""";
    cmd.Parameters.AddWithValue("l", login);
    await using var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine(await r.ReadAsync()
        ? $"Удалён: {r["Login"]} (и все связанные данные через CASCADE)"
        : $"Аккаунт '{login}' не найден.");
    return;
}

// Passwords
string adminPass = BCrypt.Net.BCrypt.HashPassword("Admin@1234");
string userPass  = BCrypt.Net.BCrypt.HashPassword("User@1234");

// Placeholder TelegramID for test users (they won't be able to login via OTP)
// Admin and User1 TelegramID will be updated later with real chat ID
const string FAKE_TG = "000000000";
const string PLACEHOLDER = "REPLACE_WITH_REAL_CHAT_ID";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

Console.WriteLine("Подключение к БД успешно.");
Console.WriteLine("Очищаю старые тестовые данные...");

// Clean up
await using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"
        DELETE FROM ""Reviews"";
        DELETE FROM ""Deals"";
        DELETE FROM ""Applications"";
        DELETE FROM ""SkillOffers"";
        DELETE FROM ""SkillRequests"";
        DELETE FROM ""VerificationRequests"";
        DELETE FROM ""Proofs"";
        DELETE FROM ""Educations"";
        DELETE FROM ""UserSkills"";
        DELETE FROM ""UserProfiles"";
        DELETE FROM ""Notifications"";
        DELETE FROM ""Accounts"";
        DELETE FROM ""SkillsCatalog"";
    ";
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Создаю каталог навыков...");

var skills = new[]
{
    ("Python",           2 /* IT */),
    ("JavaScript",       2 /* IT */),
    ("C#",               2 /* IT */),
    ("React",            2 /* IT */),
    ("PostgreSQL",       2 /* IT */),
    ("Docker",           2 /* IT */),
    ("Figma",            1 /* Design */),
    ("UI/UX Design",     1 /* Design */),
    ("Photography",      9 /* Other */),
    ("English",          3 /* Language */),
    ("German",           3 /* Language */),
    ("Guitar",           4 /* Music */),
    ("Piano",            4 /* Music */),
    ("Cooking Italian",  2 /* Cooking - mapped to IT index 2, but let's use 0=IT */),
    ("Business Analysis",6 /* Business */),
    ("Project Management",6 /* Business */),
    ("Marketing",        6 /* Business */),
    ("Data Science",     2 /* IT */),
    ("Machine Learning", 2 /* IT */),
    ("Yoga",             5 /* Sports */),
};

// SkillEpithet: IT=0, Design=1, Cooking=2, Language=3, Music=4, Sports=5, Business=6, Education=7, Healthcare=8, Other=9
var skillIds = new Guid[skills.Length];
for (int i = 0; i < skills.Length; i++)
{
    skillIds[i] = Guid.NewGuid();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"INSERT INTO ""SkillsCatalog"" (""SkillID"", ""SkillName"", ""Epithet"") VALUES (@id, @name, @ep)";
    cmd.Parameters.AddWithValue("id", skillIds[i]);
    cmd.Parameters.AddWithValue("name", skills[i].Item1);
    cmd.Parameters.AddWithValue("ep", skills[i].Item2);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine($"Создано {skills.Length} навыков в каталоге.");

// Users data
var users = new[]
{
    // login (email), fullname, dob, description, isAdmin, tgId
    ("admin@k4k.ru",   "Александр Петров",   "1985-03-15", "Администратор системы. Опытный разработчик.",                true,  PLACEHOLDER),
    ("maria@k4k.ru",   "Мария Иванова",      "1995-07-22", "Frontend-разработчик с 5 годами опыта. Люблю React и дизайн.", false, PLACEHOLDER),
    ("dmitry@k4k.ru",  "Дмитрий Сидоров",   "1990-11-08", "Backend-разработчик. Специализируюсь на .NET и PostgreSQL.",  false, FAKE_TG),
    ("anna@k4k.ru",    "Анна Козлова",       "1998-04-30", "Data Scientist, работаю с ML-моделями.",                      false, FAKE_TG),
    ("ivan@k4k.ru",    "Иван Новиков",       "1993-09-12", "Fullstack-разработчик. Люблю чистый код.",                    false, FAKE_TG),
    ("elena@k4k.ru",   "Елена Смирнова",     "1997-02-17", "UI/UX дизайнер, работаю в Figma.",                            false, FAKE_TG),
    ("sergey@k4k.ru",  "Сергей Попов",       "1988-06-25", "DevOps-инженер. Docker, Kubernetes, CI/CD.",                  false, FAKE_TG),
    ("julia@k4k.ru",   "Юлия Лебедева",      "1996-12-03", "Бизнес-аналитик, PMI сертифицирован.",                       false, FAKE_TG),
    ("mikhail@k4k.ru", "Михаил Орлов",       "1991-08-19", "Преподаватель математики и программирования.",                false, FAKE_TG),
    ("tatyana@k4k.ru", "Татьяна Федорова",   "1994-05-07", "Маркетолог с опытом в digital-направлении.",                  false, FAKE_TG),
    ("andrey@k4k.ru",  "Андрей Волков",      "1987-10-28", "Machine Learning инженер, PhD в области ИИ.",                 false, FAKE_TG),
    ("olga@k4k.ru",    "Ольга Захарова",     "1999-01-14", "Веб-дизайнер, фотограф-любитель.",                            false, FAKE_TG),
    ("pavel@k4k.ru",   "Павел Морозов",      "1992-07-05", "Python-разработчик, занимаюсь автоматизацией.",               false, FAKE_TG),
    ("natalia@k4k.ru", "Наталья Соколова",   "1995-03-21", "Преподаватель английского языка, C2 уровень.",                false, FAKE_TG),
    ("roman@k4k.ru",   "Роман Кузнецов",     "1989-11-16", "Project Manager, 10 лет в IT.",                               false, FAKE_TG),
    ("valeria@k4k.ru", "Валерия Попова",     "1997-08-09", "iOS-разработчик, Swift и SwiftUI.",                           false, FAKE_TG),
    ("artem@k4k.ru",   "Артём Васильев",     "1993-04-02", "Гитарист и звукорежиссёр.",                                   false, FAKE_TG),
    ("kris@k4k.ru",    "Кристина Белова",    "1998-09-27", "Нутрициолог и фитнес-тренер, преподаю йогу.",                false, FAKE_TG),
    ("nikita@k4k.ru",  "Никита Ромасов",     "1991-06-13", "Повар итальянской кухни, веду мастер-классы.",                false, FAKE_TG),
    ("vika@k4k.ru",    "Вика Громова",       "1996-12-20", "Java-разработчик, Spring Boot, микросервисы.",                false, FAKE_TG),
};

var accountIds = new Guid[users.Length];
Console.WriteLine("Создаю аккаунты и профили...");

for (int i = 0; i < users.Length; i++)
{
    var (login, fullName, dob, desc, isAdmin, tgId) = users[i];
    accountIds[i] = Guid.NewGuid();
    var hash = isAdmin ? adminPass : userPass;
    var now = DateTime.UtcNow;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            INSERT INTO ""Accounts"" (""AccountID"", ""Login"", ""PasswordHash"", ""TelegramID"", ""IsAdmin"", ""IsActive"", ""NotificationsEnabled"", ""FailedLoginAttempts"", ""CreatedAt"")
            VALUES (@id, @login, @hash, @tgId, @admin, true, true, 0, @now)";
        cmd.Parameters.AddWithValue("id", accountIds[i]);
        cmd.Parameters.AddWithValue("login", login);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("tgId", tgId);
        cmd.Parameters.AddWithValue("admin", isAdmin);
        cmd.Parameters.AddWithValue("now", now);
        await cmd.ExecuteNonQueryAsync();
    }

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            INSERT INTO ""UserProfiles"" (""AccountID"", ""FullName"", ""DateOfBirth"", ""Description"", ""IsActive"")
            VALUES (@id, @name, @dob, @desc, true)";
        cmd.Parameters.AddWithValue("id", accountIds[i]);
        cmd.Parameters.AddWithValue("name", fullName);
        cmd.Parameters.AddWithValue("dob", DateTime.Parse(dob));
        cmd.Parameters.AddWithValue("desc", desc);
        await cmd.ExecuteNonQueryAsync();
    }
}

Console.WriteLine($"Создано {users.Length} аккаунтов.");

// Education
Console.WriteLine("Добавляю образование...");
var educationData = new[]
{
    // (userIndex, institution, degree, year)
    (0,  "МГУ им. Ломоносова",             "Информатика и вычислительная техника", 2007),
    (1,  "МГТУ им. Баумана",               "Программная инженерия",                2017),
    (1,  "Яндекс.Практикум",               "Frontend-разработка",                  2020),
    (2,  "НИУ ВШЭ",                        "Прикладная математика",                2012),
    (3,  "СПбГУ",                          "Математика и Computer Science",         2020),
    (4,  "ИТМО",                           "Информационные системы",               2015),
    (5,  "Британская высшая школа дизайна","UI/UX Design",                         2019),
    (6,  "МГТУ им. Баумана",               "Системное программирование",           2010),
    (7,  "РЭУ им. Плеханова",              "Бизнес-информатика",                   2016),
    (8,  "МГУ им. Ломоносова",             "Математика",                           2013),
    (9,  "НИУ ВШЭ",                        "Маркетинг",                            2016),
    (10, "Сколтех",                        "Искусственный интеллект",              2013),
    (10, "MIT OpenCourseWare",             "Machine Learning",                     2018),
    (11, "Московская школа дизайна",       "Графический дизайн",                   2021),
    (12, "МФТИ",                           "Прикладная физика и математика",       2014),
    (13, "МГЛУ",                           "Лингвистика, английский язык",         2017),
    (14, "РАНХиГС",                        "Управление проектами",                 2011),
    (15, "НИУ ВШЭ",                        "Разработка мобильных приложений",      2019),
    (16, "Гнесинская академия",            "Музыкальное искусство",               2015),
    (17, "РНИМУ им. Пирогова",             "Диетология и нутрициология",           2020),
    (18, "Московский кулинарный колледж",  "Технология приготовления пищи",        2013),
    (19, "МГТУ им. Баумана",               "Программная инженерия",                2013),
};

foreach (var (idx, inst, degree, year) in educationData)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO ""Educations"" (""EducationID"", ""AccountID"", ""InstitutionName"", ""DegreeField"", ""YearCompleted"")
        VALUES (@eid, @aid, @inst, @deg, @year)";
    cmd.Parameters.AddWithValue("eid", Guid.NewGuid());
    cmd.Parameters.AddWithValue("aid", accountIds[idx]);
    cmd.Parameters.AddWithValue("inst", inst);
    cmd.Parameters.AddWithValue("deg", degree);
    cmd.Parameters.AddWithValue("year", year);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Добавляю навыки пользователей...");
// SkillLevel: Trainee=0, Junior=1, Middle=2, Senior=3
var userSkillData = new[]
{
    // (userIndex, skillIndex, level, description, learnedAt, isVerified)
    (0,  2,  3, "Разрабатываю на C# более 10 лет",         "2013",              true),
    (0,  4,  3, "PostgreSQL архитектура и оптимизация",     "2014",              true),
    (0,  5,  2, "Docker, docker-compose",                   "2019",              false),
    (1,  1,  3, "React, TypeScript, Redux",                  "2019",              true),
    (1,  6,  2, "Figma для прототипирования",               "2020",              false),
    (1,  9,  2, "Английский C1",                            "2010",              false),
    (2,  2,  3, "ASP.NET Core, Clean Architecture",         "2014",              true),
    (2,  4,  3, "PostgreSQL, query optimization",           "2015",              true),
    (2,  5,  2, "Docker Swarm, Kubernetes basics",          "2020",              false),
    (3,  0,  3, "Python для ML: pandas, sklearn, pytorch",  "2018",              true),
    (3, 17,  3, "Data Science: анализ и визуализация",      "2018",              true),
    (3, 18,  2, "Machine Learning модели",                  "2020",              false),
    (4,  1,  2, "JavaScript, Node.js",                      "2016",              false),
    (4,  2,  2, "C#, .NET",                                 "2017",              false),
    (4,  3,  2, "React",                                    "2019",              false),
    (5,  6,  3, "Figma, Sketch, Adobe XD",                  "2018",              true),
    (5,  7,  3, "Дизайн-системы, пользовательский опыт",   "2019",              true),
    (5,  8,  2, "Фотография, ретушь",                       "2020",              false),
    (6,  5,  3, "Docker, Kubernetes, Helm",                  "2017",              true),
    (6,  0,  1, "Python для скриптов автоматизации",        "2019",              false),
    (7, 14,  3, "Бизнес-анализ, BPMN, UML",                "2014",              true),
    (7, 15,  2, "Project Management, PMI",                  "2016",              false),
    (8,  0,  2, "Python для обучения",                      "2015",              false),
    (8,  2,  1, "C# базовый",                               "2018",              false),
    (9, 16,  3, "Digital Marketing, SEO, SMM",              "2015",              true),
    (10, 18, 3, "ML: TensorFlow, PyTorch, скики",          "2013",              true),
    (10,  0, 3, "Python — основной язык",                   "2011",              true),
    (10, 17, 3, "Data Science, статистика",                 "2012",              true),
    (11,  6, 2, "Figma, Adobe Illustrator",                 "2020",              false),
    (11,  8, 3, "Фотография: портрет, пейзаж",             "2018",              true),
    (12,  0, 3, "Python: автоматизация, скрапинг, API",    "2014",              true),
    (12,  4, 2, "PostgreSQL, SQLAlchemy",                   "2016",              false),
    (13,  9, 3, "English C2 — преподаватель",               "2008",              true),
    (13, 10, 2, "Немецкий B2",                              "2015",              false),
    (14, 15, 3, "Project Management, Agile, Scrum",        "2011",              true),
    (14, 14, 2, "Бизнес-анализ",                           "2013",              false),
    (15,  1, 2, "JavaScript/TypeScript",                   "2020",              false),
    (15,  3, 1, "React basics",                             "2021",              false),
    (16, 11, 3, "Гитара: рок, блюз, джаз",                "2010",              true),
    (16, 12, 2, "Фортепиано классическое",                  "2008",              false),
    (17, 19, 3, "Йога, медитация, пранаяма",               "2016",              true),
    (18,  13,3, "Итальянская кухня, паста, ризотто",       "2010",              true),
    (19,  1, 2, "Java, Spring Boot",                        "2015",              false),
    (19,  4, 2, "PostgreSQL",                               "2016",              false),
};

foreach (var (uIdx, sIdx, level, desc, learnedAt, verified) in userSkillData)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO ""UserSkills"" (""AccountID"", ""SkillID"", ""SkillLevel"", ""Description"", ""LearnedAt"", ""IsVerified"")
        VALUES (@aid, @sid, @level, @desc, @la, @ver)";
    cmd.Parameters.AddWithValue("aid", accountIds[uIdx]);
    cmd.Parameters.AddWithValue("sid", skillIds[sIdx]);
    cmd.Parameters.AddWithValue("level", level);
    cmd.Parameters.AddWithValue("desc", desc);
    cmd.Parameters.AddWithValue("la", learnedAt);
    cmd.Parameters.AddWithValue("ver", verified);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Добавляю объявления об обмене навыками...");
var offerData = new[]
{
    // (userIndex, skillIndex, title, details)
    (0,  2, "Обучение C# и .NET архитектуре", "Провожу менторинг по C#, ASP.NET Core, Clean Architecture. 1-2 часа в неделю."),
    (1,  1, "React и TypeScript — с нуля до уверенного уровня", "Помогу освоить React, хуки, Redux. Практические проекты."),
    (2,  4, "PostgreSQL: оптимизация запросов и индексы", "Разбираем сложные запросы, explain analyze, партиционирование."),
    (3,  0, "Python для Data Science", "Обучу pandas, matplotlib, sklearn. Анализ реальных датасетов."),
    (5,  6, "Figma: от макета до прототипа", "Научу работать с компонентами, автолейаутом, вариантами."),
    (6,  5, "Docker и Kubernetes с нуля", "Контейнеризация приложений, docker-compose, деплой в K8s."),
    (7, 14, "Бизнес-анализ и BPMN", "Помогу освоить моделирование бизнес-процессов."),
    (10, 18,"Machine Learning практика", "TensorFlow, PyTorch, создание и обучение нейросетей."),
    (12,  0,"Python автоматизация", "Скрипты, парсинг, работа с API, автоматизация рутины."),
    (13,  9,"Английский: разговорная практика", "Conversation club, подготовка к IELTS/TOEFL."),
    (14, 15,"Project Management для IT", "Agile, Scrum, Kanban. Подготовка к PMP."),
    (16, 11,"Игра на гитаре", "Обучение с нуля или улучшение техники. Рок, блюз, акустика."),
    (17, 19,"Йога и медитация", "Онлайн или оффлайн занятия. Начинающие и опытные."),
    (18, 13,"Итальянская кухня", "Мастер-классы по пасте, ризотто, тирамису."),
};

foreach (var (uIdx, sIdx, title, details) in offerData)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO ""SkillOffers"" (""OfferID"", ""AccountID"", ""SkillID"", ""Title"", ""Details"", ""IsActive"", ""CreatedAt"")
        VALUES (@oid, @aid, @sid, @title, @details, true, @now)";
    cmd.Parameters.AddWithValue("oid", Guid.NewGuid());
    cmd.Parameters.AddWithValue("aid", accountIds[uIdx]);
    cmd.Parameters.AddWithValue("sid", skillIds[sIdx]);
    cmd.Parameters.AddWithValue("title", title);
    cmd.Parameters.AddWithValue("details", details);
    cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("Добавляю запросы на навыки...");
var requestData = new[]
{
    (1,  2,  "Ищу ментора по React Suspense и Server Components"),
    (2,  0,  "Хочу изучить Python для автоматизации задач"),
    (3,  1,  "Нужна помощь с TypeScript и React"),
    (4,  6,  "Хочу научиться проектировать в Figma"),
    (8,  0,  "Ищу курс по Python для Data Science"),
    (9, 15,  "Хочу изучить основы Project Management"),
    (11, 9,  "Ищу разговорный клуб по английскому"),
    (15, 11, "Хочу научиться играть на гитаре"),
    (19,  7, "Нужен ментор по UI/UX"),
};

foreach (var (uIdx, sIdx, title) in requestData)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO ""SkillRequests"" (""RequestID"", ""AccountID"", ""SkillID"", ""Title"", ""Status"", ""CreatedAt"")
        VALUES (@rid, @aid, @sid, @title, 0, @now)";
    cmd.Parameters.AddWithValue("rid", Guid.NewGuid());
    cmd.Parameters.AddWithValue("aid", accountIds[uIdx]);
    cmd.Parameters.AddWithValue("sid", skillIds[sIdx]);
    cmd.Parameters.AddWithValue("title", title);
    cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine();
Console.WriteLine("=== ГОТОВО ===");
Console.WriteLine();
Console.WriteLine("Аккаунты созданы:");
Console.WriteLine($"  ADMIN:  login=admin@k4k.ru  | пароль=Admin@1234  | TelegramID={PLACEHOLDER}");
Console.WriteLine($"  USER1:  login=maria@k4k.ru  | пароль=User@1234   | TelegramID={PLACEHOLDER}");
Console.WriteLine($"  USER2+: login=dmitry@k4k.ru и т.д. | пароль=User@1234   | TelegramID={FAKE_TG} (фейк, войти нельзя)");
Console.WriteLine();
Console.WriteLine("!!! Обнови TelegramID для admin и user1 после получения chat ID:");
Console.WriteLine($"  UPDATE \"Accounts\" SET \"TelegramID\"='ВАШ_CHAT_ID' WHERE \"Login\" IN ('admin', 'user1');");
Console.WriteLine();
Console.WriteLine($"Навыков в каталоге: {skills.Length}");
Console.WriteLine($"Навыков у пользователей: {userSkillData.Length}");
Console.WriteLine($"Записей об образовании: {educationData.Length}");
Console.WriteLine($"Объявлений об обмене: {offerData.Length}");
Console.WriteLine($"Запросов на навыки: {requestData.Length}");
