import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import MatchScoreBadge from '../components/MatchScoreBadge';
import SkillTag from '../components/SkillTag';
import { candidatesApi, jobsApi, matchingApi } from '../services/api';
import { Candidate, Job, MatchResult } from '../types';

const formatDate = (value?: string | null) => {
  if (!value) {
    return 'Present';
  }

  return new Date(value).toLocaleDateString();
};

function CandidateDetail() {
  const { id = '' } = useParams();
  const [candidate, setCandidate] = useState<Candidate | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [matches, setMatches] = useState<MatchResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [matching, setMatching] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const loadCandidate = async () => {
      try {
        setLoading(true);
        setError('');
        const [candidateResponse, jobsResponse, matchResponse] = await Promise.all([
          candidatesApi.getById(id),
          jobsApi.getAll(),
          candidatesApi.getMatches(id).catch(() => [] as MatchResult[]),
        ]);

        setCandidate(candidateResponse);
        setJobs(jobsResponse);
        setMatches(matchResponse);
      } catch {
        setError('Unable to load the candidate profile.');
      } finally {
        setLoading(false);
      }
    };

    void loadCandidate();
  }, [id]);

  const handleFindMatches = async () => {
    try {
      setMatching(true);
      setError('');
      const results = await matchingApi.matchCandidateToAllJobs(id);
      setMatches(results);
    } catch {
      setError('Unable to calculate job matches right now.');
    } finally {
      setMatching(false);
    }
  };

  const jobsById = useMemo(() => new Map(jobs.map((job) => [job.id, job])), [jobs]);

  if (loading) {
    return <div className="page"><p className="loading-state">Loading candidate profile...</p></div>;
  }

  if (!candidate) {
    return <div className="page"><div className="empty-state">Candidate not found.</div></div>;
  }

  return (
    <div className="page stack-gap-lg">
      <div className="page-header">
        <div>
          <Link to="/candidates" className="back-link">← Back to candidates</Link>
          <h1>
            {candidate.firstName} {candidate.lastName}
          </h1>
          <p className="page-description">{candidate.summary || 'No summary available yet.'}</p>
        </div>
        <button className="button" type="button" onClick={handleFindMatches} disabled={matching}>
          {matching ? 'Finding matches...' : 'Find Matching Jobs'}
        </button>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <section className="detail-grid">
        <article className="section-card stack-gap">
          <div>
            <p className="section-label">Contact</p>
            <h2>Profile overview</h2>
          </div>
          <div className="info-list">
            <div>
              <span>Email</span>
              <strong>{candidate.email || 'Not provided'}</strong>
            </div>
            <div>
              <span>Phone</span>
              <strong>{candidate.phone || 'Not provided'}</strong>
            </div>
            <div>
              <span>CV file</span>
              <strong>{candidate.cvFileUrl || 'Not uploaded'}</strong>
            </div>
          </div>
          <div>
            <p className="section-label">Skills</p>
            <div className="tag-list">
              {candidate.skills.length > 0 ? (
                candidate.skills.map((skill) => <SkillTag key={skill} skill={skill} />)
              ) : (
                <span className="muted-text">No skills extracted.</span>
              )}
            </div>
          </div>
        </article>

        <article className="section-card stack-gap">
          <div>
            <p className="section-label">AI matches</p>
            <h2>Matching jobs</h2>
          </div>
          {matches.length === 0 ? (
            <div className="empty-state">No matches available. Run the matching workflow.</div>
          ) : (
            <div className="match-list">
              {matches.map((match) => {
                const job = jobsById.get(match.jobId);
                return (
                  <div key={match.id} className="match-card match-card-vertical">
                    <div className="match-card-header">
                      <div>
                        <strong>{job?.title ?? match.jobId}</strong>
                        <p>{job?.department ?? 'Department unavailable'}</p>
                      </div>
                      <MatchScoreBadge score={match.score} level={match.matchLevel} />
                    </div>
                    <p>{match.explanation}</p>
                    <div className="tag-list tag-list-compact">
                      {match.skillMatches.map((skill) => (
                        <SkillTag key={skill} skill={skill} />
                      ))}
                    </div>
                    {match.skillGaps.length > 0 ? (
                      <p className="muted-text">Skill gaps: {match.skillGaps.join(', ')}</p>
                    ) : null}
                  </div>
                );
              })}
            </div>
          )}
        </article>
      </section>

      <section className="detail-grid">
        <article className="section-card stack-gap">
          <div>
            <p className="section-label">Career history</p>
            <h2>Experience</h2>
          </div>
          {candidate.experience.length === 0 ? (
            <div className="empty-state">No work experience extracted.</div>
          ) : (
            candidate.experience.map((experience) => (
              <div key={experience.id} className="timeline-item">
                <strong>{experience.title}</strong>
                <p>{experience.company}</p>
                <span>
                  {formatDate(experience.startDate)} - {formatDate(experience.endDate)}
                </span>
                <p>{experience.description}</p>
              </div>
            ))
          )}
        </article>

        <article className="section-card stack-gap">
          <div>
            <p className="section-label">Academic background</p>
            <h2>Education</h2>
          </div>
          {candidate.education.length === 0 ? (
            <div className="empty-state">No education records extracted.</div>
          ) : (
            candidate.education.map((item) => (
              <div key={item.id} className="timeline-item">
                <strong>{item.degree}</strong>
                <p>
                  {item.institution} · {item.fieldOfStudy}
                </p>
                <span>
                  {formatDate(item.startDate)} - {formatDate(item.endDate)}
                </span>
              </div>
            ))
          )}
        </article>
      </section>
    </div>
  );
}

export default CandidateDetail;
