import { ChangeEvent, FormEvent, KeyboardEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import SkillTag from '../components/SkillTag';
import { jobsApi } from '../services/api';

interface JobFormState {
  title: string;
  department: string;
  description: string;
  location: string;
  experienceLevel: string;
  employmentType: string;
  requiredSkills: string[];
  preferredSkills: string[];
  responsibilities: string;
  requirements: string;
  teamSize: string;
  reportingTo: string;
  salaryRange: string;
  policyContext: string;
  tone: string;
}

const initialState: JobFormState = {
  title: '',
  department: '',
  description: '',
  location: '',
  experienceLevel: 'Mid-level',
  employmentType: 'Full-time',
  requiredSkills: [],
  preferredSkills: [],
  responsibilities: '',
  requirements: '',
  teamSize: '',
  reportingTo: '',
  salaryRange: '',
  policyContext: 'Use inclusive language and keep the role clear and candidate-friendly.',
  tone: 'Professional and inclusive',
};

type SkillKey = 'requiredSkills' | 'preferredSkills';
type TextFieldKey = Exclude<keyof JobFormState, SkillKey>;

function CreateJob() {
  const navigate = useNavigate();
  const [form, setForm] = useState<JobFormState>(initialState);
  const [requiredSkillInput, setRequiredSkillInput] = useState('');
  const [preferredSkillInput, setPreferredSkillInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState('');

  const addSkills = (value: string, key: SkillKey) => {
    const nextSkills = value
      .split(',')
      .map((skill) => skill.trim())
      .filter(Boolean);

    if (nextSkills.length === 0) {
      return;
    }

    setForm((current) => ({
      ...current,
      [key]: Array.from(new Set([...current[key], ...nextSkills])),
    }));
  };

  const removeSkill = (value: string, key: SkillKey) => {
    setForm((current) => ({
      ...current,
      [key]: current[key].filter((skill) => skill !== value),
    }));
  };

  const handleSkillKeyDown = (event: KeyboardEvent<HTMLInputElement>, key: SkillKey, reset: () => void) => {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      addSkills((event.target as HTMLInputElement).value, key);
      reset();
    }
  };

  const handleSkillBlur = (value: string, key: SkillKey, reset: () => void) => {
    addSkills(value, key);
    reset();
  };

  const handleInputChange = (key: TextFieldKey) => (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    setForm((current) => ({
      ...current,
      [key]: event.target.value,
    }));
  };

  const handleGenerateDescription = async () => {
    try {
      setGenerating(true);
      setError('');
      const description = await jobsApi.generateDescription({
        title: form.title,
        department: form.department,
        location: form.location,
        experienceLevel: form.experienceLevel,
        employmentType: form.employmentType,
        requiredSkills: form.requiredSkills,
        preferredSkills: form.preferredSkills,
        responsibilities: form.responsibilities,
        requirements: form.requirements,
        teamSize: form.teamSize,
        reportingTo: form.reportingTo,
        salaryRange: form.salaryRange,
        benefits: '',
        policyContext: form.policyContext,
        tone: form.tone,
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
        requirements: '',
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
          <p className="page-description">Define the role, add rich hiring context, and generate a stronger AI description before saving.</p>
        </div>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <form className="section-card form-grid" onSubmit={handleSubmit}>
        <label className="field">
          <span>Title</span>
          <input className="input" value={form.title} onChange={handleInputChange('title')} placeholder="Senior AI Recruiter" required />
        </label>

        <label className="field">
          <span>Department</span>
          <input className="input" value={form.department} onChange={handleInputChange('department')} placeholder="Talent Acquisition" required />
        </label>

        <label className="field">
          <span>Location</span>
          <input className="input" value={form.location} onChange={handleInputChange('location')} placeholder="Remote / London" />
        </label>

        <label className="field">
          <span>Experience level</span>
          <select className="input" value={form.experienceLevel} onChange={handleInputChange('experienceLevel')}>
            <option value="Junior">Junior</option>
            <option value="Mid-level">Mid-level</option>
            <option value="Senior">Senior</option>
            <option value="Lead">Lead</option>
            <option value="Principal">Principal</option>
          </select>
        </label>

        <label className="field">
          <span>Employment type</span>
          <select className="input" value={form.employmentType} onChange={handleInputChange('employmentType')}>
            <option value="Full-time">Full-time</option>
            <option value="Part-time">Part-time</option>
            <option value="Contract">Contract</option>
            <option value="Temporary">Temporary</option>
          </select>
        </label>

        <label className="field">
          <span>Team size</span>
          <input className="input" value={form.teamSize} onChange={handleInputChange('teamSize')} placeholder="8-person engineering squad" />
        </label>

        <label className="field">
          <span>Reporting to</span>
          <input className="input" value={form.reportingTo} onChange={handleInputChange('reportingTo')} placeholder="Director of Engineering" />
        </label>

        <label className="field">
          <span>Salary range</span>
          <input className="input" value={form.salaryRange} onChange={handleInputChange('salaryRange')} placeholder="$110,000 - $135,000" />
        </label>

        <label className="field field-full">
          <span>Required skills</span>
          <input
            className="input"
            value={requiredSkillInput}
            onChange={(event) => setRequiredSkillInput(event.target.value)}
            onKeyDown={(event) => handleSkillKeyDown(event, 'requiredSkills', () => setRequiredSkillInput(''))}
            onBlur={() => handleSkillBlur(requiredSkillInput, 'requiredSkills', () => setRequiredSkillInput(''))}
            placeholder="Type one or more skills separated by commas"
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
            onBlur={() => handleSkillBlur(preferredSkillInput, 'preferredSkills', () => setPreferredSkillInput(''))}
            placeholder="Optional skills separated by commas"
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
          <span>Responsibilities</span>
          <textarea
            className="input textarea"
            value={form.responsibilities}
            onChange={handleInputChange('responsibilities')}
            placeholder="Summarize the scope, outcomes, and day-to-day responsibilities"
            rows={5}
          />
        </label>

        <label className="field field-full">
          <span>Requirements</span>
          <textarea
            className="input textarea"
            value={form.requirements}
            onChange={handleInputChange('requirements')}
            placeholder="List mandatory qualifications, certifications, or experience"
            rows={5}
          />
        </label>

        <label className="field field-full">
          <span>Policy context</span>
          <textarea
            className="input textarea"
            value={form.policyContext}
            onChange={handleInputChange('policyContext')}
            placeholder="Provide inclusion, compliance, or organization-specific guidance for the AI"
            rows={4}
          />
        </label>

        <label className="field field-full">
          <span>Description</span>
          <textarea
            className="input textarea"
            value={form.description}
            onChange={handleInputChange('description')}
            placeholder="Add a description or generate one with AI"
            rows={10}
          />
        </label>

        <div className="field field-full button-row">
          <button className="button button-secondary" type="button" onClick={handleGenerateDescription} disabled={generating || !form.title || !form.department}>
            {generating ? 'Generating...' : 'Generate with AI'}
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
