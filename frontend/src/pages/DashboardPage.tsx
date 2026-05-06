import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';

import { useAuth } from '../auth/useAuth';
import { SkillPicker } from '../components/SkillPicker';
import { Avatar, EmptyState, LoadingBlock, Notice, StatusTag, Surface } from '../components/Ui';
import {
  addEducation,
  addUserSkill,
  createSkill,
  deleteSkillRequest,
  deleteEducation,
  getCurrentAccount,
  getEducations,
  getProfile,
  getProofs,
  getSkillRequests,
  getSkills,
  getUserSkills,
  getVerificationRequests,
  removeUserSkill,
  reviewVerificationRequest,
  resolveAssetUrl,
  submitVerificationRequest,
  upsertProfile,
  uploadProfilePhoto,
  uploadProof,
} from '../lib/api';
import {
  formatDate,
  formatRequestStatus,
  formatSkillEpithet,
  formatSkillLevel,
  formatVerificationRequestType,
  formatVerificationStatus,
} from '../lib/format';
import { normalizeTelegramContact } from '../lib/telegram';
import { skillEpithetOptions, skillLevelOptions, type Session } from '../lib/types';
import { useAsyncData } from '../lib/useAsyncData';
import {
  currentYear,
  emptySkillRequestsPage,
  getVerificationTone,
  maxBirthDate,
  maxEducationYear,
  maxProfileContactLength,
  maxProfileDescriptionLength,
  maxProfileFullNameLength,
  minBirthDate,
  validateBirthDate,
  validateEducationYear,
  validateProfilePhotoFile,
  validateSelectedProofFiles,
  verificationRequestTypeSkill,
  verificationStatusApproved,
  verificationStatusPending,
  verificationStatusRejected,
} from './dashboard/dashboardValidation';

type NoticeState = { message: string; tone: 'danger' | 'info' | 'success' } | null;

export function DashboardPage() {
  const { session } = useAuth();

  if (!session) {
    return (
      <Surface title="Личный кабинет недоступен">
        <EmptyState
          action={
            <Link className="button button--primary" to="/auth">
              Войти в систему
            </Link>
          }
          description="Чтобы управлять профилем, навыками и образованием, сначала открой сессию."
          title="Нужна авторизация"
        />
      </Surface>
    );
  }

  return <DashboardContent session={session} />;
}

function DashboardContent({ session }: { session: Session }) {
  const skillFormRef = useRef<HTMLFormElement | null>(null);
  const [refreshToken, setRefreshToken] = useState(0);
  const [notice, setNotice] = useState<NoticeState>(null);
  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [profileForm, setProfileForm] = useState({
    contactInfo: '',
    dateOfBirth: '',
    description: '',
    fullName: '',
    photoUrl: '',
  });
  const [profilePhotoFile, setProfilePhotoFile] = useState<File | null>(null);
  const [profilePhotoInputKey, setProfilePhotoInputKey] = useState(0);
  const [skillForm, setSkillForm] = useState({
    description: '',
    learnedAt: '',
    level: 0,
    skillId: '',
  });
  const [skillProofFiles, setSkillProofFiles] = useState<File[]>([]);
  const [proofInputKey, setProofInputKey] = useState(0);
  const [educationForm, setEducationForm] = useState({
    degreeField: '',
    institutionName: '',
    yearCompleted: '',
  });
  const [adminSkillForm, setAdminSkillForm] = useState({
    epithet: 0,
    skillName: '',
  });

  const accountState = useAsyncData([session.accountId, refreshToken], () => getCurrentAccount(session.token));
  const profileState = useAsyncData([session.accountId, refreshToken], () => getProfile(session.accountId, session.token));
  const catalogState = useAsyncData([refreshToken], () => getSkills({ pageSize: 500 }));
  const userSkillsState = useAsyncData([session.accountId, refreshToken], () => getUserSkills(session.accountId, session.token));
  const proofsState = useAsyncData([session.accountId, refreshToken], () => getProofs(session.accountId, session.token));
  const verificationRequestsState = useAsyncData([session.accountId, session.isAdmin, refreshToken], () =>
    getVerificationRequests(session.token, session.isAdmin ? { pageSize: 40 } : { accountId: session.accountId, pageSize: 40 }),
  );
  const adminRequestsState = useAsyncData([session.isAdmin, refreshToken], () =>
    session.isAdmin ? getSkillRequests({ pageSize: 24 }) : Promise.resolve(emptySkillRequestsPage),
  );
  const educationState = useAsyncData([session.accountId, refreshToken], () => getEducations(session.accountId, session.token));

  useEffect(() => {
    if (!profileState.data) {
      return;
    }

    setProfileForm({
      contactInfo: profileState.data.contactInfo ?? '',
      dateOfBirth: profileState.data.dateOfBirth?.slice(0, 10) ?? '',
      description: profileState.data.description ?? '',
      fullName: profileState.data.fullName ?? '',
      photoUrl: profileState.data.photoUrl ?? '',
    });
  }, [profileState.data]);

  useEffect(() => {
    if (!skillForm.skillId || !catalogState.data?.items.length) {
      return;
    }

    const skillStillExists = catalogState.data.items.some((catalogSkill) => catalogSkill.skillId === skillForm.skillId);

    if (!skillStillExists) {
      setSkillForm((current) => ({
        ...current,
        skillId: '',
      }));
    }
  }, [catalogState.data, skillForm.skillId]);

  useEffect(() => {
    if (!skillForm.skillId) {
      return;
    }

    const existingSkill = userSkillsState.data?.find((skill) => skill.skillId === skillForm.skillId);

    setSkillForm((current) => ({
      ...current,
      description: existingSkill?.description ?? '',
      learnedAt: existingSkill?.learnedAt ?? '',
      level: existingSkill?.level ?? 0,
    }));
  }, [skillForm.skillId, userSkillsState.data]);

  function refreshAll() {
    setRefreshToken((current) => current + 1);
  }

  async function runAction(actionKey: string, action: () => Promise<void>, successMessage: string) {
    setBusyAction(actionKey);
    setNotice(null);

    try {
      await action();
      setNotice({
        message: successMessage,
        tone: 'success',
      });
      refreshAll();
    } catch (error) {
      setNotice({
        message: error instanceof Error ? error.message : 'Операция не выполнена.',
        tone: 'danger',
      });
    } finally {
      setBusyAction(null);
    }
  }

  function getProofsForSkill(skillId: string) {
    return (proofsState.data ?? []).filter((proof) => proof.skillId === skillId);
  }

  function getVerificationRequestsForProof(proofId: string) {
    return (verificationRequestsState.data?.items ?? []).filter((request) => request.proofId === proofId);
  }

  function getLatestVerificationRequestForProof(proofId: string) {
    return getVerificationRequestsForProof(proofId)[0] ?? null;
  }

  const selectedSkillProofsCount = skillForm.skillId ? getProofsForSkill(skillForm.skillId).length : 0;
  const selectedExistingSkill = userSkillsState.data?.find((skill) => skill.skillId === skillForm.skillId) ?? null;
  const selectedSkillName =
    selectedExistingSkill?.skillName ??
    catalogState.data?.items.find((skill) => skill.skillId === skillForm.skillId)?.skillName ??
    '';
  const isEditingExistingSkill = selectedExistingSkill !== null;
  const profileImageUrl = profileForm.photoUrl ? resolveAssetUrl(profileForm.photoUrl) : '';
  const profileDisplayName = profileForm.fullName.trim() || profileState.data?.fullName || 'Профиль';

  function resetSkillForm() {
    setSkillForm({
      description: '',
      learnedAt: '',
      level: 0,
      skillId: '',
    });
    setSkillProofFiles([]);
    setProofInputKey((current) => current + 1);
  }

  function focusSkillForm() {
    requestAnimationFrame(() => {
      skillFormRef.current?.scrollIntoView({
        behavior: 'smooth',
        block: 'nearest',
      });
    });
  }

  function handleSkillSelection(skillId: string) {
    setSkillForm((current) => ({ ...current, skillId }));
    setSkillProofFiles([]);
    setProofInputKey((current) => current + 1);
    setNotice(null);
  }

  function beginSkillEdit(skillId: string) {
    const skillToEdit = userSkillsState.data?.find((skill) => skill.skillId === skillId);

    if (!skillToEdit) {
      return;
    }

    setSkillForm({
      description: skillToEdit.description ?? '',
      learnedAt: skillToEdit.learnedAt ?? '',
      level: skillToEdit.level,
      skillId: skillToEdit.skillId,
    });
    setSkillProofFiles([]);
    setProofInputKey((current) => current + 1);
    setNotice(null);
    focusSkillForm();
  }

  function clearProfilePhoto() {
    setProfilePhotoFile(null);
    setProfilePhotoInputKey((current) => current + 1);
    setProfileForm((current) => ({
      ...current,
      photoUrl: '',
    }));
    setNotice(null);
  }

  async function handleProfileSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedFullName = profileForm.fullName.trim();
    const normalizedDescription = profileForm.description.replace(/\r\n/g, '\n');
    const birthDateError = validateBirthDate(profileForm.dateOfBirth);
    const photoFileError = validateProfilePhotoFile(profilePhotoFile);

    if (!normalizedFullName) {
      setNotice({
        message: 'Укажи ФИО для публичной карточки.',
        tone: 'danger',
      });
      return;
    }

    if (birthDateError) {
      setNotice({
        message: birthDateError,
        tone: 'danger',
      });
      return;
    }

    if (photoFileError) {
      setNotice({
        message: photoFileError,
        tone: 'danger',
      });
      return;
    }

    await runAction('profile-save', async () => {
      const normalizedContactInfo = normalizeTelegramContact(profileForm.contactInfo.trim());
      let uploadedPhotoUrl = profileForm.photoUrl || undefined;

      if (profilePhotoFile) {
        const uploadedPhoto = await uploadProfilePhoto(session.token, profilePhotoFile);
        uploadedPhotoUrl = uploadedPhoto.photoUrl;
      }

      await upsertProfile(session.token, {
        contactInfo: normalizedContactInfo || undefined,
        dateOfBirth: profileForm.dateOfBirth || undefined,
        description: normalizedDescription.trim().length > 0 ? normalizedDescription : undefined,
        fullName: normalizedFullName,
        photoUrl: uploadedPhotoUrl,
      });

      setProfileForm((current) => ({
        ...current,
        photoUrl: uploadedPhotoUrl ?? '',
      }));
      setProfilePhotoFile(null);
      setProfilePhotoInputKey((current) => current + 1);
    }, 'Профиль сохранён.');
  }

  async function handleSkillSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!skillForm.skillId) {
      setNotice({
        message: 'Сначала выбери навык из каталога.',
        tone: 'danger',
      });
      return;
    }

    const proofFileError = validateSelectedProofFiles(skillProofFiles, selectedSkillProofsCount);

    if (proofFileError) {
      setNotice({
        message: proofFileError,
        tone: 'danger',
      });
      return;
    }

    setBusyAction('skill-add');
    setNotice(null);

    try {
      const isUpdatingSkill = Boolean(userSkillsState.data?.some((skill) => skill.skillId === skillForm.skillId));

      await addUserSkill(session.token, {
        description: skillForm.description.trim() || undefined,
        learnedAt: skillForm.learnedAt.trim() || undefined,
        level: skillForm.level,
        skillId: skillForm.skillId,
      });

      let nextNotice: NoticeState = {
        message: isUpdatingSkill ? 'Изменения по навыку сохранены.' : 'Навык сохранён.',
        tone: 'success',
      };

      if (skillProofFiles.length > 0) {
        try {
          for (const proofFile of skillProofFiles) {
            await uploadProof(session.token, {
              file: proofFile,
              skillId: skillForm.skillId,
            });
          }

          nextNotice = {
            message:
              skillProofFiles.length === 1
                ? isUpdatingSkill
                  ? 'Изменения по навыку сохранены, приложение загружено.'
                  : 'Навык сохранён, приложение загружено.'
                : isUpdatingSkill
                  ? `Изменения по навыку сохранены, приложений загружено: ${skillProofFiles.length}.`
                  : `Навык сохранён, приложений загружено: ${skillProofFiles.length}.`,
            tone: 'success',
          };
          setSkillProofFiles([]);
          setProofInputKey((current) => current + 1);
        } catch (error) {
          nextNotice = {
            message: `Навык сохранён, но приложения не загрузились: ${error instanceof Error ? error.message : 'неизвестная ошибка.'}`,
            tone: 'danger',
          };
        }
      }

      setNotice(nextNotice);
      refreshAll();
    } catch (error) {
      setNotice({
        message: error instanceof Error ? error.message : 'Операция не выполнена.',
        tone: 'danger',
      });
    } finally {
      setBusyAction(null);
    }
  }

  async function handleSubmitProofForVerification(proofId: string) {
    await runAction(`proof-verify-submit-${proofId}`, async () => {
      await submitVerificationRequest(session.token, {
        proofId,
        requestType: verificationRequestTypeSkill,
      });
    }, 'Пруф отправлен администратору на проверку.');
  }

  async function handleEducationSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedInstitutionName = educationForm.institutionName.trim();
    const yearError = validateEducationYear(educationForm.yearCompleted);

    if (!normalizedInstitutionName) {
      setNotice({
        message: 'Укажи место обучения.',
        tone: 'danger',
      });
      return;
    }

    if (yearError) {
      setNotice({
        message: yearError,
        tone: 'danger',
      });
      return;
    }

    await runAction('education-add', async () => {
      await addEducation(session.token, {
        degreeField: educationForm.degreeField.trim() || undefined,
        institutionName: normalizedInstitutionName,
        yearCompleted: educationForm.yearCompleted ? Number(educationForm.yearCompleted) : undefined,
      });

      setEducationForm({
        degreeField: '',
        institutionName: '',
        yearCompleted: '',
      });
    }, 'Запись об образовании сохранена.');
  }

  async function handleAdminSkillCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedSkillName = adminSkillForm.skillName.trim();

    if (!normalizedSkillName) {
      setNotice({
        message: 'Для каталога навыков нужно указать название.',
        tone: 'danger',
      });
      return;
    }

    await runAction('admin-skill-create', async () => {
      await createSkill(session.token, {
        epithet: adminSkillForm.epithet,
        skillName: normalizedSkillName,
      });

      setAdminSkillForm({
        epithet: 0,
        skillName: '',
      });
    }, 'Новый навык добавлен в каталог.');
  }

  return (
    <div className="page-stack">
      <Surface className="dashboard-hero">
        <div className="hero-grid hero-grid--compact">
          <div className="hero-copy">
            <h1>Личный кабинет пользователя</h1>
            <p className="hero-text">
              Здесь заполняется только то, что описывает твою публичную карточку: профиль, навыки и образование.
            </p>
          </div>
          <div className="detail-panel">
            <div className="button-row">
              <Avatar imageUrl={profileImageUrl} name={profileDisplayName} size="lg" />
              <div>
                <span className="eyebrow">Профиль</span>
                <strong>{profileState.data?.fullName || 'Заполни карточку о себе'}</strong>
              </div>
            </div>
            <div className="button-row">
              <Link className="button button--ghost" to={`/profile/${session.accountId}`}>
                Публичный профиль
              </Link>
              <StatusTag label={accountState.data?.isAdmin ? 'Admin' : 'Member'} tone="accent" />
            </div>
          </div>
        </div>
      </Surface>

      {notice ? <Notice message={notice.message} tone={notice.tone} /> : null}

      <div className="page-stack--two-column">
        <div className="page-stack">
          <Surface description="Основная информация публичной карточки пользователя." title="Профиль">
            {profileState.loading ? (
              <LoadingBlock label="Загружаем профиль..." />
            ) : (
              <form className="form-grid" onSubmit={handleProfileSubmit}>
                <label>
                  <span>Фото профиля</span>
                  <div className="button-row">
                    <Avatar imageUrl={profileImageUrl} name={profileDisplayName} size="lg" />
                    <div>
                      <strong>{profileDisplayName}</strong>
                      <p className="meta-line">
                        {profilePhotoFile
                          ? `Будет загружен новый файл: ${profilePhotoFile.name}`
                          : profileForm.photoUrl
                            ? 'Текущее фото уже загружено.'
                            : 'Пока без фотографии.'}
                        </p>
                        {(profileForm.photoUrl || profilePhotoFile) && (
                          <button className="button button--ghost" onClick={clearProfilePhoto} type="button">
                            Удалить фото
                          </button>
                        )}
                      </div>
                    </div>
                    <input
                      accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
                      key={profilePhotoInputKey}
                    onChange={(event) => setProfilePhotoFile(event.target.files?.[0] ?? null)}
                      type="file"
                    />
                    <small className="field-hint">JPEG, PNG или WebP до 5 МБ.</small>
                    {profileState.data?.photoUrl && !profileForm.photoUrl && !profilePhotoFile ? (
                      <small className="field-hint">Если сохранить профиль сейчас, фотография в карточке будет удалена.</small>
                    ) : null}
                  </label>
                <label>
                  <span>ФИО</span>
                  <input
                    maxLength={maxProfileFullNameLength}
                    onChange={(event) => setProfileForm((current) => ({ ...current, fullName: event.target.value }))}
                    placeholder="Иван Петров"
                    value={profileForm.fullName}
                  />
                  <small className="field-hint">До 150 символов.</small>
                </label>
                <label>
                  <span>Дата рождения</span>
                  <input
                    max={maxBirthDate}
                    min={minBirthDate}
                    onChange={(event) => setProfileForm((current) => ({ ...current, dateOfBirth: event.target.value }))}
                    type="date"
                    value={profileForm.dateOfBirth}
                  />
                  <small className="field-hint">Если не хочешь показывать дату рождения, оставь поле пустым.</small>
                </label>
                <label>
                  <span>Контакт для связи</span>
                  <input
                    maxLength={maxProfileContactLength}
                    onChange={(event) => setProfileForm((current) => ({ ...current, contactInfo: event.target.value }))}
                    placeholder="@telegram, t.me/username или другой контакт"
                    value={profileForm.contactInfo}
                  />
                  <small className="field-hint">До 255 символов.</small>
                </label>
                <label>
                  <span>Личное описание</span>
                  <textarea
                    maxLength={maxProfileDescriptionLength}
                    onChange={(event) => setProfileForm((current) => ({ ...current, description: event.target.value }))}
                    placeholder="Кто ты, чем занимаешься, что умеешь объяснить и чему хочешь научиться."
                    rows={5}
                    value={profileForm.description}
                  />
                  <small className="field-hint">До 3000 символов. Переносы строк и табуляция сохраняются.</small>
                </label>
                <button className="button button--primary" disabled={busyAction === 'profile-save'} type="submit">
                  {busyAction === 'profile-save' ? 'Сохраняем...' : 'Сохранить профиль'}
                </button>
              </form>
            )}
          </Surface>

          <Surface description="Добавь учебные места и направления, которые стоит показывать в публичной карточке." title="Образование">
            {educationState.loading ? (
              <LoadingBlock label="Загружаем образование..." />
            ) : educationState.data && educationState.data.length > 0 ? (
              <div className="list-stack">
                {educationState.data.map((education) => (
                  <article className="list-card" key={education.educationId}>
                    <div className="list-card-top">
                      <strong>{education.institutionName}</strong>
                      {education.yearCompleted ? <StatusTag label={String(education.yearCompleted)} tone="neutral" /> : null}
                    </div>
                    <p>{education.degreeField || 'Направление пока не указано.'}</p>
                    <button
                      className="button button--ghost"
                      onClick={() =>
                        runAction(
                          `education-remove-${education.educationId}`,
                          () => deleteEducation(session.token, education.educationId),
                          'Запись об образовании удалена.',
                        )
                      }
                      type="button"
                    >
                      Удалить
                    </button>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyState description="Добавь вуз, колледж, школу или курс, которые стоит показать в профиле." title="Образование пока не заполнено" />
            )}

            <form className="form-grid" onSubmit={handleEducationSubmit}>
              <label>
                <span>Место обучения</span>
                <input
                  onChange={(event) => setEducationForm((current) => ({ ...current, institutionName: event.target.value }))}
                  placeholder="Например: НИУ ВШЭ, МГТУ, Яндекс Практикум"
                  value={educationForm.institutionName}
                />
              </label>
              <label>
                <span>Направление / программа</span>
                <input
                  onChange={(event) => setEducationForm((current) => ({ ...current, degreeField: event.target.value }))}
                  placeholder="Например: прикладная информатика, UX/UI design"
                  value={educationForm.degreeField}
                />
              </label>
              <label>
                <span>Год окончания</span>
                <input
                  inputMode="numeric"
                  max={maxEducationYear}
                  min={currentYear - 120}
                  onChange={(event) => setEducationForm((current) => ({ ...current, yearCompleted: event.target.value }))}
                  placeholder="2027"
                  value={educationForm.yearCompleted}
                />
              </label>
              <button className="button button--primary" disabled={busyAction === 'education-add'} type="submit">
                {busyAction === 'education-add' ? 'Сохраняем...' : 'Добавить образование'}
              </button>
            </form>
          </Surface>
        </div>

        <Surface description="Навыки пользователя, их уровень и краткий контекст." title="Мои навыки">
          {userSkillsState.loading ? (
            <LoadingBlock label="Загружаем навыки..." />
          ) : userSkillsState.data && userSkillsState.data.length > 0 ? (
            <div className="skill-grid">
              {userSkillsState.data.map((skill) => {
                const skillProofs = getProofsForSkill(skill.skillId);
                const isCurrentSkillEditing = skill.skillId === skillForm.skillId && isEditingExistingSkill;

                return (
                  <article className="list-card skill-card" key={skill.skillId}>
                    <div className="list-card-top">
                      <strong>{skill.skillName}</strong>
                      <div className="button-row">
                        {isCurrentSkillEditing ? <StatusTag label="Редактируется" tone="warning" /> : null}
                        <StatusTag label={formatSkillLevel(skill.level)} tone="accent" />
                      </div>
                    </div>
                    <p>{skill.description || 'Краткое описание пока не добавлено.'}</p>
                    <p className="meta-line">Где изучал: {skill.learnedAt || 'Не указано'}</p>
                    <p className="meta-line">Приложения: {skillProofs.length > 0 ? String(skillProofs.length) : 'пока не загружены'}</p>
                    {skillProofs.length > 0 ? (
                      <div className="list-stack">
                        {skillProofs.map((proof, index) => {
                          const latestVerificationRequest = getLatestVerificationRequestForProof(proof.proofId);
                          const canSubmitForVerification =
                            !proof.isVerified && latestVerificationRequest?.status !== verificationStatusPending;

                          return (
                            <div className="list-card" key={proof.proofId}>
                              <div className="list-card-top">
                                <strong>Приложение {index + 1}</strong>
                                <StatusTag
                                  label={
                                    proof.isVerified
                                      ? 'Подтверждён админом'
                                      : latestVerificationRequest
                                        ? formatVerificationStatus(latestVerificationRequest.status)
                                        : 'Не отправлен'
                                  }
                                  tone={
                                    proof.isVerified
                                      ? 'success'
                                      : getVerificationTone(latestVerificationRequest?.status)
                                  }
                                />
                              </div>
                              <div className="button-row">
                                <a
                                  className="button button--ghost"
                                  href={resolveAssetUrl(proof.fileUrl)}
                                  rel="noreferrer"
                                  target="_blank"
                                >
                                  Открыть файл
                                </a>
                                {canSubmitForVerification ? (
                                  <button
                                    className="button button--ghost"
                                    onClick={() => handleSubmitProofForVerification(proof.proofId)}
                                    type="button"
                                  >
                                    {latestVerificationRequest?.status === verificationStatusRejected
                                      ? 'Отправить повторно'
                                      : 'Отправить на проверку'}
                                  </button>
                                ) : null}
                              </div>
                              {latestVerificationRequest ? (
                                <p className="meta-line">
                                  Последняя заявка: {formatVerificationRequestType(latestVerificationRequest.requestType)} ·{' '}
                                  {formatDate(latestVerificationRequest.createdAt)}
                                </p>
                              ) : (
                                <p className="meta-line">Этот пруф ещё не отправлялся на проверку.</p>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    ) : null}
                    <div className="button-row">
                      <button className="button button--ghost" onClick={() => beginSkillEdit(skill.skillId)} type="button">
                        Редактировать
                      </button>
                      <button
                        className="button button--ghost"
                        onClick={() =>
                          runAction(`skill-remove-${skill.skillId}`, () => removeUserSkill(session.token, skill.skillId), 'Навык удалён.')
                        }
                        type="button"
                      >
                        Удалить
                      </button>
                    </div>
                  </article>
                );
              })}
            </div>
          ) : (
            <EmptyState
              description="Добавь хотя бы один навык, чтобы потом можно было публиковать карточки в каталоге."
              title="Навыков пока нет"
            />
          )}

          {skillForm.skillId ? (
            <div className="detail-panel">
              <span className="eyebrow">{isEditingExistingSkill ? 'Редактирование навыка' : 'Новый навык'}</span>
              <strong>{selectedSkillName || 'Выбранный навык'}</strong>
              <p>
                {isEditingExistingSkill
                  ? 'Можно дополнить описание, место изучения, уровень и сразу пересохранить навык.'
                  : 'Заполни карточку навыка, и он сразу появится в твоём профиле.'}
              </p>
            </div>
          ) : null}

          <form className="form-grid" onSubmit={handleSkillSubmit} ref={skillFormRef}>
            <label>
              <span>Навык</span>
              <SkillPicker
                emptyMessage="Такого навыка в каталоге пока нет."
                onChange={handleSkillSelection}
                options={catalogState.data?.items ?? []}
                placeholder="Например: React, Docker, PostgreSQL"
                value={skillForm.skillId}
              />
              <small className="field-hint">
                Выбери существующий навык, чтобы отредактировать его, или новый из каталога, чтобы добавить в профиль.
              </small>
            </label>
            <label>
              <span>Уровень</span>
              <select
                onChange={(event) => setSkillForm((current) => ({ ...current, level: Number(event.target.value) }))}
                value={skillForm.level}
              >
                {skillLevelOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Краткое описание</span>
              <textarea
                onChange={(event) => setSkillForm((current) => ({ ...current, description: event.target.value }))}
                placeholder="Например: умею разбирать backend, собирать API и объяснять базовую архитектуру."
                rows={3}
                value={skillForm.description}
              />
            </label>
            <label>
              <span>Где изучал</span>
              <input
                onChange={(event) => setSkillForm((current) => ({ ...current, learnedAt: event.target.value }))}
                placeholder="Например: университет, курс, практика, pet-проект"
                value={skillForm.learnedAt}
              />
            </label>
            <label>
              <span>Пруф к навыку</span>
              <input
                accept=".jpg,.jpeg,.png,.webp,.pdf,image/jpeg,image/png,image/webp,application/pdf"
                key={proofInputKey}
                multiple
                onChange={(event) => setSkillProofFiles(Array.from(event.target.files ?? []))}
                type="file"
              />
              <small className="field-hint">
                {skillProofFiles.length > 0
                  ? `Выбрано файлов: ${skillProofFiles.length}. ${skillProofFiles.map((file) => file.name).join(', ')}`
                  : 'Можно сразу приложить до 3 файлов: JPEG, PNG, WebP или PDF до 10 МБ каждый.'}
              </small>
              <small className="field-hint">
                Можно приложить до 3 файлов на один навык. Сейчас выбрано: {skillProofFiles.length}.
              </small>
              {selectedSkillProofsCount > 0 ? (
                <small className="field-hint">
                  У этого навыка уже есть приложений: {selectedSkillProofsCount}. Новые файлы просто добавятся к ним, пока общий лимит не превысит 3.
                </small>
              ) : null}
            </label>
            <div className="button-row">
              <button className="button button--primary" disabled={busyAction === 'skill-add'} type="submit">
                {busyAction === 'skill-add'
                  ? 'Сохраняем...'
                  : isEditingExistingSkill
                    ? 'Сохранить изменения'
                    : 'Сохранить навык'}
              </button>
              {skillForm.skillId ? (
                <button className="button button--ghost" onClick={resetSkillForm} type="button">
                  {isEditingExistingSkill ? 'Отменить редактирование' : 'Очистить форму'}
                </button>
              ) : null}
            </div>
          </form>
        </Surface>
      </div>

      {session.isAdmin ? (
        <>
          <div className="content-grid content-grid--wide">
            <Surface
              description="Только администратор может одобрять или отклонять пруфы пользователей."
              title="Админ: Проверка пруфов"
            >
              {verificationRequestsState.loading ? (
                <LoadingBlock label="Загружаем заявки на проверку..." />
              ) : verificationRequestsState.data && verificationRequestsState.data.items.length > 0 ? (
                <div className="list-stack">
                  {verificationRequestsState.data.items.map((request) => (
                    <article className="list-card" key={request.requestId}>
                      <div className="list-card-top">
                        <div>
                          <strong>{request.accountName}</strong>
                          <span className="meta-line">
                            {formatVerificationRequestType(request.requestType)}
                            {request.skillName ? ` · ${request.skillName}` : ''}
                          </span>
                        </div>
                        <StatusTag label={formatVerificationStatus(request.status)} tone={getVerificationTone(request.status)} />
                      </div>
                      <p className="meta-line">Подана: {formatDate(request.createdAt)}</p>
                      {request.proofFileUrl ? (
                        <div className="button-row">
                          <a className="button button--ghost" href={resolveAssetUrl(request.proofFileUrl)} rel="noreferrer" target="_blank">
                            Открыть пруф
                          </a>
                          {request.status === verificationStatusPending ? (
                            <>
                              <button
                                className="button button--primary"
                                onClick={() =>
                                  runAction(
                                    `verification-approve-${request.requestId}`,
                                    () => reviewVerificationRequest(session.token, request.requestId, verificationStatusApproved),
                                    'Пруф подтверждён администратором.',
                                  )
                                }
                                type="button"
                              >
                                Подтвердить
                              </button>
                              <button
                                className="button button--ghost"
                                onClick={() =>
                                  runAction(
                                    `verification-reject-${request.requestId}`,
                                    () => reviewVerificationRequest(session.token, request.requestId, verificationStatusRejected),
                                    'Пруф отклонён.',
                                  )
                                }
                                type="button"
                              >
                                Отклонить
                              </button>
                            </>
                          ) : null}
                        </div>
                      ) : (
                        <p className="meta-line">К этой заявке не приложен файл-пруф.</p>
                      )}
                    </article>
                  ))}
                </div>
              ) : (
                <EmptyState description="Сейчас у админа нет заявок на проверку." title="Очередь пуста" />
              )}
            </Surface>

            <Surface
              description="Админ может расширять общий каталог навыков для всех пользователей."
              title="Админ: Каталог навыков"
            >
              <form className="form-grid" onSubmit={handleAdminSkillCreate}>
                <label>
                  <span>Название навыка</span>
                  <input
                    onChange={(event) => setAdminSkillForm((current) => ({ ...current, skillName: event.target.value }))}
                    placeholder="Например: Node.js, Blender, Academic Writing"
                    value={adminSkillForm.skillName}
                  />
                </label>
                <label>
                  <span>Категория</span>
                  <select
                    onChange={(event) => setAdminSkillForm((current) => ({ ...current, epithet: Number(event.target.value) }))}
                    value={adminSkillForm.epithet}
                  >
                    {skillEpithetOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </label>
                <button className="button button--primary" disabled={busyAction === 'admin-skill-create'} type="submit">
                  {busyAction === 'admin-skill-create' ? 'Добавляем...' : 'Добавить навык в каталог'}
                </button>
              </form>

              {catalogState.data?.items.length ? (
                <div className="chip-cloud">
                  {catalogState.data.items.slice(0, 10).map((skill) => (
                    <div className="skill-chip" key={skill.skillId}>
                      <strong>{skill.skillName}</strong>
                      <span>{formatSkillEpithet(skill.epithet)}</span>
                    </div>
                  ))}
                </div>
              ) : null}
            </Surface>
          </div>

          <Surface
            description="Администратор может удалить любую карточку запроса, если она ошибочна или больше не нужна."
            title="Админ: Запросы пользователей"
          >
            {adminRequestsState.loading ? (
              <LoadingBlock label="Загружаем карточки запросов..." />
            ) : adminRequestsState.data && adminRequestsState.data.items.length > 0 ? (
              <div className="list-stack">
                {adminRequestsState.data.items.map((request) => (
                  <article className="list-card" key={request.requestId}>
                    <div className="list-card-top">
                      <div>
                        <strong>{request.title}</strong>
                        <span className="meta-line">
                          {request.authorName} · {request.skillName}
                        </span>
                      </div>
                      <StatusTag label={formatRequestStatus(request.status)} tone="accent" />
                    </div>
                    <p>{request.details || 'Автор не добавил описание.'}</p>
                    <div className="button-row">
                      <Link className="button button--ghost" to={`/profile/${request.accountId}`}>
                        Открыть автора
                      </Link>
                      <button
                        className="button button--ghost"
                        onClick={() =>
                          runAction(
                            `admin-request-delete-${request.requestId}`,
                            () => deleteSkillRequest(session.token, request.requestId),
                            'Карточка запроса удалена администратором.',
                          )
                        }
                        type="button"
                      >
                        Удалить карточку
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <EmptyState description="В системе пока нет запросов на изучение навыков." title="Карточек нет" />
            )}
          </Surface>
        </>
      ) : null}
    </div>
  );
}

