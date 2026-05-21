import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import FileUpload from '../components/FileUpload';
import SkillTag from '../components/SkillTag';
import { candidatesApi } from '../services/api';
import { Candidate } from '../types';

const formatDate = (value: string) => new Date(value).toLocaleDateString();

function CandidatesPage() {
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [uploadedCandidate, setUploadedCandidate] = useState<Candidate | null>(null);
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
      const parsedCandidate = await candidatesApi.uploadCv(file);
      setUploadedCandidate(parsedCandidate);
      await loadCandidates();
    } catch {
      setError('CV upload failed. Please try again with a supported file.');
    } finally {
      setUploading(false);
    }
  };

  const sortedCandidates = useMemo(
    () =>
      [...candidates].sort(
        (left, right) =>
          new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
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
            Upload CVs, review parsed profiles, and open candidate detail views to explore AI
            matching results.
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
        <FileUpload onFileSelect={handleUpload} loading={uploading} />
        {uploadedCandidate ? (
          <div className="highlight-card">
            <div>
              <p className="section-label">Parsed profile</p>
              <h3>
                {uploadedCandidate.firstName} {uploadedCandidate.lastName}
              </h3>
            </div>
            <p>{uploadedCandidate.summary || 'Candidate summary is not available yet.'}</p>
            <div className="tag-list">
              {uploadedCandidate.skills.map((skill) => (
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
                  <th>Created</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {sortedCandidates.map((candidate) => (
                  <tr key={candidate.id}>
                    <td>
                      {candidate.firstName} {candidate.lastName}
                    </td>
                    <td>{candidate.email || '—'}</td>
                    <td>{candidate.phone || '—'}</td>
                    <td>
                      <div className="tag-list tag-list-compact">
                        {candidate.skills.slice(0, 3).map((skill) => (
                          <SkillTag key={skill} skill={skill} />
                        ))}
                      </div>
                    </td>
                    <td>{formatDate(candidate.createdAt)}</td>
                    <td>
                      <Link className="button button-secondary" to={`/candidates/${candidate.id}`}>
                        View profile
                      </Link>
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
