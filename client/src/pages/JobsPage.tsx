import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import SkillTag from '../components/SkillTag';
import { jobsApi } from '../services/api';
import { Job } from '../types';

function JobsPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

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

  const activeJobs = useMemo(() => jobs.filter((job) => job.isActive), [jobs]);

  return (
    <div className="page stack-gap-lg">
      <div className="page-header">
        <div>
          <p className="section-label">Job management</p>
          <h1>Open roles</h1>
          <p className="page-description">
            Manage job openings, required skills, and candidate matching activity.
          </p>
        </div>
        <Link to="/jobs/create" className="button">
          Create Job
        </Link>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}
      {loading ? <p className="loading-state">Loading active jobs...</p> : null}
      {!loading && activeJobs.length === 0 ? (
        <div className="empty-state">No active jobs found. Create a role to begin matching.</div>
      ) : null}

      <section className="card-grid">
        {activeJobs.map((job) => (
          <article key={job.id} className="section-card stack-gap">
            <div className="match-card-header">
              <div>
                <p className="section-label">{job.department || 'General'}</p>
                <h2>{job.title}</h2>
              </div>
              <Link className="button button-secondary" to={`/jobs/${job.id}`}>
                View details
              </Link>
            </div>
            <p>{job.description || 'Description will be generated or added later.'}</p>
            <div>
              <p className="section-label">Required skills</p>
              <div className="tag-list">
                {job.requiredSkills.map((skill) => (
                  <SkillTag key={skill} skill={skill} />
                ))}
              </div>
            </div>
          </article>
        ))}
      </section>
    </div>
  );
}

export default JobsPage;
