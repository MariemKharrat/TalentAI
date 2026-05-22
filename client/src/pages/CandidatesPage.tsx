import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import FileUpload from '../components/FileUpload';
import SkillTag from '../components/SkillTag';
import { candidatesApi } from '../services/api';
import { Candidate, CvParsingMethod } from '../types';

const formatDate = (value?: string) => (value ? new Date(value).toLocaleDateString() : '—');
const formatParsingMethod = (value?: string) =>
  value === 'DocumentIntelligence'
    ? 'Document Intelligence (OCR-based)'
    : 'Content Understanding (AI-powered)';
const getCandidateName = (candidate: Candidate) =>
  candidate.fullName?.trim() || `${candidate.firstName ?? ''} ${candidate.lastName ?? ''}`.trim() || 'Unnamed candidate';
const getCandidateSkills = (candidate: Candidate): string[] => {
  if (Array.isArray(candidate.skills)) return candidate.skills;
  if (typeof candidate.skills === 'string' && candidate.skills) 
    return candidate.skills.split(',').map(s => s.trim()).filter(Boolean);
  return [];
};
const getCandidateCreatedAt = (candidate: Candidate) => candidate.createdAtUtc ?? candidate.createdAt;

function CandidatesPage() {
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [uploadedCandidate, setUploadedCandidate] = useState<Candidate | null>(null);
  const [parsingMethod, setParsingMethod] = useState<CvParsingMethod>('ContentUnderstanding');
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState('');

  const loadCandidates = async () => {
    try {
      setLoading(true);
      setError('');
      const data = await candidatesApi.getAll();
      setCandidates(data);
    } catch {
      setError('Unable to load candidates. Please verify the API is available.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadCandidates();
  }, []);

  const handleUpload = async (file: File) => {
    try {
      setUploading(true);
      setError('');
      const parsedCandidate = await candidatesApi.uploadCv(file, parsingMethod);
      setUploadedCandidate(parsedCandidate);
      await loadCandidates();
    } catch {
      setError('CV upload failed. Please try again with a supported file.');
    } finally {
      setUploading(false);
    }
  };

  const deleteCandidate = async (candidate: Candidate) => {
    if (!window.confirm(`Delete "${getCandidateName(candidate)}"? This cannot be undone.`)) return;
    try {
      await candidatesApi.delete(candidate.id);
      setCandidates((prev) => prev.filter((c) => c.id !== candidate.id));
    } catch {
      setError('Failed to delete candidate.');
    }
  };

  const sortedCandidates = useMemo(
    () =>
      [...candidates].sort(
        (left, right) =>
          new Date(getCandidateCreatedAt(right) ?? 0).getTime() -
          new Date(getCandidateCreatedAt(left) ?? 0).getTime()
      ),
    [candidates]
  );

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <p className="section-label">Candidate management</p>
          <h1>Candidates</h1>
          <p className="page-description">
            Upload CVs, compare both Azure AI parsing methods, and review parsed profiles side-by-side.
          </p>
        </div>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <section className="section-card stack-gap">
        <div className="section-header">
          <div>
            <h2>Upload CV</h2>
            <p className="page-description">Supported formats: PDF, DOC, and DOCX.</p>
          </div>
        </div>
        <div className="field method-selector">
          <span>Parsing method</span>
          <select
            className="input"
            value={parsingMethod}
            onChange={(event) => setParsingMethod(event.target.value as CvParsingMethod)}
            disabled={uploading}
          >
            <option value="ContentUnderstanding">Content Understanding (AI-powered)</option>
            <option value="DocumentIntelligence">Document Intelligence (OCR-based)</option>
          </select>
        </div>
        <FileUpload onFileSelect={handleUpload} loading={uploading} />
        {uploadedCandidate ? (
          <div className="highlight-card stack-gap">
            <div>
              <p className="section-label">Parsed profile</p>
              <h3>{getCandidateName(uploadedCandidate)}</h3>
              <p className="method-note">{formatParsingMethod(uploadedCandidate.parsingMethod)}</p>
            </div>
            <p>{uploadedCandidate.summary || 'Candidate summary is not available yet.'}</p>
            <div className="tag-list">
              {getCandidateSkills(uploadedCandidate).map((skill) => (
                <SkillTag key={skill} skill={skill} />
              ))}
            </div>
          </div>
        ) : null}
      </section>

      <section className="section-card">
        <div className="section-header">
          <div>
            <h2>Candidate roster</h2>
            <p className="page-description">{sortedCandidates.length} candidates available.</p>
          </div>
        </div>
        {loading ? <p className="loading-state">Loading candidates...</p> : null}
        {!loading && sortedCandidates.length === 0 ? (
          <div className="empty-state">No candidates found. Upload a CV to get started.</div>
        ) : null}
        {!loading && sortedCandidates.length > 0 ? (
          <div className="table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Phone</th>
                  <th>Skills</th>
                  <th>Parsing method</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {sortedCandidates.map((candidate) => (
                  <tr key={candidate.id}>
                    <td>{getCandidateName(candidate)}</td>
                    <td>{candidate.email || '—'}</td>
                    <td>{candidate.phone || '—'}</td>
                    <td>
                      <div className="tag-list tag-list-compact">
                        {getCandidateSkills(candidate).slice(0, 3).map((skill) => (
                          <SkillTag key={skill} skill={skill} />
                        ))}
                      </div>
                    </td>
                    <td>{formatParsingMethod(candidate.parsingMethod)}</td>
                    <td>{formatDate(getCandidateCreatedAt(candidate))}</td>
                    <td>
                      <div className="button-row">
                        <Link className="button button-secondary" to={`/candidates/${candidate.id}`}>
                          View profile
                        </Link>
                        <button
                          className="button button-small button-danger"
                          onClick={() => deleteCandidate(candidate)}
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>
    </div>
  );
}

export default CandidatesPage;
