import { FormEvent, KeyboardEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import SkillTag from '../components/SkillTag';
import { jobsApi } from '../services/api';

interface JobFormState {
  title: string;
  department: string;
  description: string;
  experienceLevel: string;
  location: string;
  policyContext: string;
  requiredSkills: string[];
  preferredSkills: string[];
}

const initialState: JobFormState = {
  title: '',
  department: '',
  description: '',
  experienceLevel: '',
  location: '',
  policyContext: 'Use inclusive language and keep the role clear and candidate-friendly.',
  requiredSkills: [],
  preferredSkills: [],
};

function CreateJob() {
  const navigate = useNavigate();
  const [form, setForm] = useState<JobFormState>(initialState);
  const [requiredSkillInput, setRequiredSkillInput] = useState('');
  const [preferredSkillInput, setPreferredSkillInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState('');

  const addSkill = (value: string, key: 'requiredSkills' | 'preferredSkills') => {
    const trimmedValue = value.trim();
    if (!trimmedValue || form[key].includes(trimmedValue)) {
      return;
    }

    setForm((current) => ({
      ...current,
      [key]: [...current[key], trimmedValue],
    }));
  };

  const removeSkill = (value: string, key: 'requiredSkills' | 'preferredSkills') => {
    setForm((current) => ({
      ...current,
      [key]: current[key].filter((skill) => skill !== value),
    }));
  };

  const handleSkillKeyDown = (
    event: KeyboardEvent<HTMLInputElement>,
    key: 'requiredSkills' | 'preferredSkills',
    reset: () => void
  ) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      addSkill((event.target as HTMLInputElement).value, key);
      reset();
    }
  };

  const handleGenerateDescription = async () => {
    try {
      setGenerating(true);
      setError('');
      const description = await jobsApi.generateDescription({
        title: form.title,
        department: form.department,
        requiredSkills: form.requiredSkills,
        experienceLevel: form.experienceLevel,
        policyContext: form.policyContext,
      });
      setForm((current) => ({ ...current, description }));
    } catch {
      setError('Unable to generate the job description right now.');
    } finally {
      setGenerating(false);
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    try {
      setLoading(true);
      setError('');
      const createdJob = await jobsApi.create({
        title: form.title,
        description: form.description,
        department: form.department,
        requiredSkills: form.requiredSkills,
        preferredSkills: form.preferredSkills,
        experienceLevel: form.experienceLevel,
        location: form.location,
        isActive: true,
      });
      navigate(`/jobs/${createdJob.id}`);
    } catch {
      setError('Job creation failed. Please review the form and try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page stack-gap-lg">
      <div className="page-header">
        <div>
          <Link to="/jobs" className="back-link">← Back to jobs</Link>
          <h1>Create job</h1>
          <p className="page-description">Define the role, add skill requirements, and optionally generate an AI description before saving.</p>
        </div>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <form className="section-card form-grid" onSubmit={handleSubmit}>
        <label className="field">
          <span>Title</span>
          <input
            className="input"
            value={form.title}
            onChange={(event) => setForm({ ...form, title: event.target.value })}
            placeholder="Senior AI Recruiter"
            required
          />
        </label>

        <label className="field">
          <span>Department</span>
          <input
            className="input"
            value={form.department}
            onChange={(event) => setForm({ ...form, department: event.target.value })}
            placeholder="Talent Acquisition"
            required
          />
        </label>

        <label className="field">
          <span>Experience level</span>
          <input
            className="input"
            value={form.experienceLevel}
            onChange={(event) => setForm({ ...form, experienceLevel: event.target.value })}
            placeholder="Mid-Senior"
            required
          />
        </label>

        <label className="field">
          <span>Location</span>
          <input
            className="input"
            value={form.location}
            onChange={(event) => setForm({ ...form, location: event.target.value })}
            placeholder="Remote / London"
          />
        </label>

        <label className="field field-full">
          <span>Required skills</span>
          <input
            className="input"
            value={requiredSkillInput}
            onChange={(event) => setRequiredSkillInput(event.target.value)}
            onKeyDown={(event) => handleSkillKeyDown(event, 'requiredSkills', () => setRequiredSkillInput(''))}
            placeholder="Type a skill and press Enter"
          />
          <div className="tag-list">
            {form.requiredSkills.map((skill) => (
              <button type="button" key={skill} className="tag-button" onClick={() => removeSkill(skill, 'requiredSkills')}>
                <SkillTag skill={skill} />
              </button>
            ))}
          </div>
        </label>

        <label className="field field-full">
          <span>Preferred skills</span>
          <input
            className="input"
            value={preferredSkillInput}
            onChange={(event) => setPreferredSkillInput(event.target.value)}
            onKeyDown={(event) => handleSkillKeyDown(event, 'preferredSkills', () => setPreferredSkillInput(''))}
            placeholder="Optional skills to highlight"
          />
          <div className="tag-list">
            {form.preferredSkills.map((skill) => (
              <button type="button" key={skill} className="tag-button" onClick={() => removeSkill(skill, 'preferredSkills')}>
                <SkillTag skill={skill} />
              </button>
            ))}
          </div>
        </label>

        <label className="field field-full">
          <span>Policy context for AI description</span>
          <input
            className="input"
            value={form.policyContext}
            onChange={(event) => setForm({ ...form, policyContext: event.target.value })}
            placeholder="Describe the tone or policy context"
          />
        </label>

        <label className="field field-full">
          <span>Description</span>
          <textarea
            className="input textarea"
            value={form.description}
            onChange={(event) => setForm({ ...form, description: event.target.value })}
            placeholder="Add a description or generate one with AI"
            rows={8}
          />
        </label>

        <div className="field field-full button-row">
          <button className="button button-secondary" type="button" onClick={handleGenerateDescription} disabled={generating || !form.title}>
            {generating ? 'Generating...' : 'Generate AI Description'}
          </button>
          <button className="button" type="submit" disabled={loading}>
            {loading ? 'Saving...' : 'Save Job'}
          </button>
        </div>
      </form>
    </div>
  );
}

export default CreateJob;
