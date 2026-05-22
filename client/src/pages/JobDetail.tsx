import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import MatchScoreBadge from '../components/MatchScoreBadge';
import SkillTag from '../components/SkillTag';
import { candidatesApi, jobsApi, matchingApi } from '../services/api';
import { Candidate, Job, MatchResult } from '../types';

function JobDetail() {
  const { id = '' } = useParams();
  const [job, setJob] = useState<Job | null>(null);
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [matches, setMatches] = useState<MatchResult[]>([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [matching, setMatching] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const loadJob = async () => {
      try {
        setLoading(true);
        setError('');
        const [jobResponse, candidatesResponse, existingMatches] = await Promise.all([
          jobsApi.getById(id),
          candidatesApi.getAll(),
          jobsApi.getCandidates(id).catch(() => [] as MatchResult[]),
        ]);

        setJob(jobResponse);
        setCandidates(candidatesResponse);
        setMatches(existingMatches);
      } catch {
        setError('Unable to load this job.');
      } finally {
        setLoading(false);
      }
    };

    void loadJob();
  }, [id]);

  const handleGenerateDescription = async () => {
    if (!job) {
      return;
    }

    try {
      setGenerating(true);
      setError('');
      const description = await jobsApi.generateDescription({
        title: job.title,
        department: job.department,
        location: job.location || '',
        experienceLevel: job.experienceLevel || '',
        employmentType: 'Full-time',
        requiredSkills: job.requiredSkills || [],
        preferredSkills: job.preferredSkills || [],
        responsibilities: job.description,
        requirements: '',
        teamSize: '',
        reportingTo: '',
        salaryRange: '',
        benefits: '',
        policyContext: 'Create an inclusive, concise role summary for recruiters.',
        tone: 'Professional and inclusive',
      });
      setJob({ ...job, description });
    } catch {
      setError('Description generation failed.');
    } finally {
      setGenerating(false);
    }
  };

  const handleFindCandidates = async () => {
    try {
      setMatching(true);
      setError('');
      const results = await matchingApi.matchJobToAllCandidates(id);
      setMatches(results);
    } catch {
      setError('Unable to find matching candidates.');
    } finally {
      setMatching(false);
    }
  };

  const candidatesById = useMemo(
    () => new Map(candidates.map((candidate) => [candidate.id, candidate])),
    [candidates]
  );

  if (loading) {
    return <div className="page"><p className="loading-state">Loading job details...</p></div>;
  }

  if (!job) {
    return <div className="page"><div className="empty-state">Job not found.</div></div>;
  }

  return (
    <div className="page stack-gap-lg">
      <div className="page-header">
        <div>
          <Link to="/jobs" className="back-link">← Back to jobs</Link>
          <h1>{job.title}</h1>
          <p className="page-description">{job.department || 'General department'} · {job.location || 'Location TBD'}</p>
        </div>
        <div className="button-row">
          <button className="button button-secondary" type="button" onClick={handleGenerateDescription} disabled={generating}>
            {generating ? 'Generating...' : 'Generate Description with AI'}
          </button>
          <button className="button" type="button" onClick={handleFindCandidates} disabled={matching}>
            {matching ? 'Matching...' : 'Find Matching Candidates'}
          </button>
        </div>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <section className="detail-grid">
        <article className="section-card stack-gap">
          <div>
            <p className="section-label">Role overview</p>
            <h2>Description</h2>
          </div>
          <p>{job.description || 'Generate an AI-powered description or add one manually.'}</p>
          <div className="info-list">
            <div>
              <span>Experience level</span>
              <strong>{job.experienceLevel || 'Not set'}</strong>
            </div>
            <div>
              <span>Status</span>
              <strong>{job.isActive ? 'Active' : 'Inactive'}</strong>
            </div>
          </div>
        </article>

        <article className="section-card stack-gap">
          <div>
            <p className="section-label">Skills</p>
            <h2>Hiring criteria</h2>
          </div>
          <div>
            <p className="section-label">Required</p>
            <div className="tag-list">
              {(job.requiredSkills || []).length > 0 ? (job.requiredSkills || []).map((skill) => <SkillTag key={skill} skill={skill} />) : <span className="muted-text">No required skills added.</span>}
            </div>
          </div>
          <div>
            <p className="section-label">Preferred</p>
            <div className="tag-list">
              {(job.preferredSkills || []).length > 0 ? (job.preferredSkills || []).map((skill) => <SkillTag key={skill} skill={skill} />) : <span className="muted-text">No preferred skills added.</span>}
            </div>
          </div>
        </article>
      </section>

      <section className="section-card stack-gap">
        <div>
          <p className="section-label">AI results</p>
          <h2>Candidate matches</h2>
        </div>
        {matches.length === 0 ? (
          <div className="empty-state">No candidate matches yet. Run the matching workflow.</div>
        ) : (
          <div className="match-list">
            {matches.map((match) => {
              const candidate = candidatesById.get(match.candidateId);
              return (
                <div key={match.id} className="match-card match-card-vertical">
                  <div className="match-card-header">
                    <div>
                      <strong>
                        {candidate ? `${candidate.firstName} ${candidate.lastName}` : match.candidateId}
                      </strong>
                      <p>{candidate?.email ?? 'Email unavailable'}</p>
                    </div>
                    <MatchScoreBadge score={match.score} level={match.matchLevel} />
                  </div>
                  <p>{match.explanation}</p>
                  {(match.skillMatches || []).length > 0 ? (
                    <div className="tag-list tag-list-compact">
                      {(match.skillMatches || []).map((skill) => (
                        <SkillTag key={skill} skill={skill} />
                      ))}
                    </div>
                  ) : null}
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

export default JobDetail;
