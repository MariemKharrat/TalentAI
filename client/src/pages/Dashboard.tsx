import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { candidatesApi, jobsApi } from '../services/api';
import MatchScoreBadge from '../components/MatchScoreBadge';
import { Job, MatchResult } from '../types';

function Dashboard() {
  const [totalCandidates, setTotalCandidates] = useState(0);
  const [activeJobs, setActiveJobs] = useState(0);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [recentMatches, setRecentMatches] = useState<MatchResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const loadDashboard = async () => {
      try {
        setLoading(true);
        setError('');

        const [candidates, jobsResponse] = await Promise.all([
          candidatesApi.getAll(),
          jobsApi.getAll(),
        ]);

        const matches = (
          await Promise.all(
            candidates.map(async (candidate) => {
              try {
                return await candidatesApi.getMatches(candidate.id);
              } catch {
                return [] as MatchResult[];
              }
            })
          )
        )
          .flat()
          .sort(
            (left, right) =>
              new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
          )
          .slice(0, 5);

        setTotalCandidates(candidates.length);
        setJobs(jobsResponse);
        setActiveJobs(jobsResponse.filter((job) => job.isActive).length);
        setRecentMatches(matches);
      } catch (loadError) {
        setError('Unable to load dashboard data. Please ensure the API is running.');
      } finally {
        setLoading(false);
      }
    };

    void loadDashboard();
  }, []);

  const jobsById = useMemo(
    () => new Map(jobs.map((job) => [job.id, job.title])),
    [jobs]
  );

  return (
    <div className="page stack-gap-lg">
      <section className="hero-card">
        <div>
          <p className="eyebrow">Recruitment intelligence</p>
          <h1>Make faster, smarter hiring decisions</h1>
          <p className="hero-text">
            Upload CVs, manage open roles, and surface AI-powered candidate matches from a
            single dashboard.
          </p>
        </div>
      </section>

      {error ? <div className="error-banner">{error}</div> : null}

      <section className="stats-grid">
        <article className="stat-card">
          <span>Total candidates</span>
          <strong>{loading ? '...' : totalCandidates}</strong>
        </article>
        <article className="stat-card">
          <span>Active jobs</span>
          <strong>{loading ? '...' : activeJobs}</strong>
        </article>
        <article className="stat-card">
          <span>Recent matches</span>
          <strong>{loading ? '...' : recentMatches.length}</strong>
        </article>
      </section>

      <section className="card-grid">
        <Link to="/candidates" className="action-card">
          <h2>Candidates</h2>
          <p>Upload CVs, review parsed profiles, and launch job matching workflows.</p>
        </Link>
        <Link to="/jobs" className="action-card">
          <h2>Jobs</h2>
          <p>Create active openings, generate AI descriptions, and manage required skills.</p>
        </Link>
      </section>

      <section className="section-card">
        <div className="section-header">
          <div>
            <p className="section-label">Live insights</p>
            <h2>Recent job matches</h2>
          </div>
        </div>
        {loading ? <p className="loading-state">Loading matches...</p> : null}
        {!loading && recentMatches.length === 0 ? (
          <div className="empty-state">No match activity yet. Start from a candidate profile.</div>
        ) : null}
        <div className="match-list">
          {recentMatches.map((match) => (
            <div key={match.id} className="match-card">
              <div>
                <p className="section-label">Job</p>
                <strong>{jobsById.get(match.jobId) ?? match.jobId}</strong>
              </div>
              <MatchScoreBadge score={match.score} level={match.matchLevel} />
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

export default Dashboard;
