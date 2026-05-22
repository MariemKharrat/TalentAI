import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import SkillTag from '../components/SkillTag';
import { jobsApi } from '../services/api';
import { Job } from '../types';

type StatusFilter = 'all' | 'active' | 'closed';

function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState<StatusFilter>('active');

  useEffect(() => {
    const loadJobs = async () => {
      try {
        setLoading(true);
        setError('');
        const data = await jobsApi.getAll();
        setJobs(data);
      } catch {
        setError('Unable to load jobs. Please verify the API is running.');
      } finally {
        setLoading(false);
      }
    };

    void loadJobs();
  }, []);

  const filteredJobs = useMemo(() => {
    if (filter === 'active') return jobs.filter((job) => job.isActive);
    if (filter === 'closed') return jobs.filter((job) => !job.isActive);
    return jobs;
  }, [jobs, filter]);

  const toggleStatus = async (job: Job) => {
    try {
      const updated = await jobsApi.update(job.id, {
        title: job.title,
        description: job.description,
        department: job.department,
        requirements: job.requirements,
        requiredSkills: job.requiredSkills,
        preferredSkills: job.preferredSkills,
        location: job.location,
        experienceLevel: job.experienceLevel,
        isActive: !job.isActive,
      });
      setJobs((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
    } catch {
      // silently fail
    }
  };

  const deleteJob = async (job: Job) => {
    if (!window.confirm(`Delete "${job.title}"? This cannot be undone.`)) return;
    try {
      await jobsApi.delete(job.id);
      setJobs((prev) => prev.filter((j) => j.id !== job.id));
    } catch {
      setError('Failed to delete job.');
    }
  };

  return (
    <div className="page stack-gap-lg">
      <div className="page-header">
        <div>
          <p className="section-label">Job management</p>
          <h1>All roles</h1>
          <p className="page-description">
            Manage job openings, required skills, and candidate matching activity.
          </p>
        </div>
        <Link to="/jobs/create" className="button">
          Create Job
        </Link>
      </div>

      <div className="filter-tabs">
        <button className={`tab ${filter === 'active' ? 'tab-active' : ''}`} onClick={() => setFilter('active')}>
          Active ({jobs.filter((j) => j.isActive).length})
        </button>
        <button className={`tab ${filter === 'closed' ? 'tab-active' : ''}`} onClick={() => setFilter('closed')}>
          Closed ({jobs.filter((j) => !j.isActive).length})
        </button>
        <button className={`tab ${filter === 'all' ? 'tab-active' : ''}`} onClick={() => setFilter('all')}>
          All ({jobs.length})
        </button>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}
      {loading ? <p className="loading-state">Loading jobs...</p> : null}
      {!loading && filteredJobs.length === 0 ? (
        <div className="empty-state">No jobs found for this filter.</div>
      ) : null}

      <section className="job-list">
        {filteredJobs.map((job) => (
          <article key={job.id} className={`job-list-item ${!job.isActive ? 'card-inactive' : ''}`}>
            <div className="job-list-header">
              <div className="job-list-info">
                <div className="job-list-title-row">
                  <h2>{job.title}</h2>
                  <span className={`status-badge ${job.isActive ? 'status-active' : 'status-closed'}`}>
                    {job.isActive ? 'Active' : 'Closed'}
                  </span>
                </div>
                <p className="job-list-meta">
                  {job.department || 'General'} {job.location ? `• ${job.location}` : ''} {job.experienceLevel ? `• ${job.experienceLevel}` : ''}
                </p>
                <p className="job-list-description">
                  {job.description ? job.description.slice(0, 200) + (job.description.length > 200 ? '...' : '') : 'No description yet.'}
                </p>
                <div className="tag-list">
                  {(job.requiredSkills || []).map((skill) => (
                    <SkillTag key={skill} skill={skill} />
                  ))}
                  {!job.requiredSkills?.length && job.requirements && (
                    <span className="text-muted">{job.requirements}</span>
                  )}
                </div>
              </div>
              <div className="job-list-actions">
                <Link className="button button-secondary" to={`/jobs/${job.id}`}>
                  View details
                </Link>
                <button
                  className={`button button-small ${job.isActive ? 'button-danger' : 'button-success'}`}
                  onClick={() => toggleStatus(job)}
                >
                  {job.isActive ? 'Close role' : 'Reactivate'}
                </button>
                <button
                  className="button button-small button-danger"
                  onClick={() => deleteJob(job)}
                >
                  Delete
                </button>
              </div>
            </div>
          </article>
        ))}
      </section>
    </div>
  );
}

export default JobsPage;
