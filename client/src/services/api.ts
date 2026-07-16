import axios from 'axios';
import { Candidate, CvParsingMethod, Job, JobDescriptionRequest, MatchResult } from '../types';

// Resolve the API base URL at runtime (window.__ENV__ is populated by the
// container entrypoint from the API_BASE_URL env var), falling back to the
// build-time env var and finally the local dev server.
declare global {
  interface Window {
    __ENV__?: { API_BASE_URL?: string };
  }
}

const API_BASE_URL =
  window.__ENV__?.API_BASE_URL ||
  process.env.REACT_APP_API_BASE_URL ||
  'http://localhost:5000';

const api = axios.create({
  baseURL: `${API_BASE_URL}/api`,
});

type CandidateApiResponse = Partial<Candidate> & {
  fullName?: string;
  createdAtUtc?: string;
  cvFileName?: string;
  skills?: string[] | string | null;
};

const splitCandidateName = (candidate: CandidateApiResponse) => {
  if (candidate.firstName || candidate.lastName) {
    return {
      firstName: candidate.firstName ?? '',
      lastName: candidate.lastName ?? '',
    };
  }

  const fullName = candidate.fullName?.trim() ?? '';
  if (!fullName) {
    return { firstName: '', lastName: '' };
  }

  const parts = fullName.split(/\s+/);
  return {
    firstName: parts[0] ?? '',
    lastName: parts.slice(1).join(' '),
  };
};

const normalizeCandidate = (candidate: CandidateApiResponse): Candidate => {
  const { firstName, lastName } = splitCandidateName(candidate);
  const skills = Array.isArray(candidate.skills)
    ? candidate.skills
    : (candidate.skills ?? '')
        .split(',')
        .map((skill) => skill.trim())
        .filter(Boolean);
  const createdAt = candidate.createdAt ?? candidate.createdAtUtc ?? '';
  const cvFileUrl = candidate.cvFileUrl ?? candidate.cvFileName ?? '';

  return {
    id: candidate.id ?? '',
    firstName,
    lastName,
    fullName: candidate.fullName ?? `${firstName} ${lastName}`.trim(),
    email: candidate.email ?? '',
    phone: candidate.phone ?? '',
    skills,
    experience: candidate.experience ?? [],
    education: candidate.education ?? [],
    summary: candidate.summary ?? '',
    cvFileUrl,
    cvFileName: candidate.cvFileName,
    createdAt,
    createdAtUtc: candidate.createdAtUtc,
    parsingMethod: candidate.parsingMethod,
  };
};

export const candidatesApi = {
  async uploadCv(file: File, method: CvParsingMethod = 'ContentUnderstanding') {
    const formData = new FormData();
    formData.append('file', file);

    const { data } = await api.post<CandidateApiResponse>(`/candidates/upload-cv?method=${method}`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return normalizeCandidate(data);
  },
  async getAll() {
    const { data } = await api.get<CandidateApiResponse[]>('/candidates');
    return data.map(normalizeCandidate);
  },
  async getById(id: string) {
    const { data } = await api.get<CandidateApiResponse>(`/candidates/${id}`);
    return normalizeCandidate(data);
  },
  async getMatches(id: string) {
    const { data } = await api.get<MatchResult[]>(`/candidates/${id}/matches`);
    return data;
  },
  getCvUrl(id: string) {
    return `${API_BASE_URL}/api/candidates/${id}/cv`;
  },
  async delete(id: string) {
    await api.delete(`/candidates/${id}`);
  },
};

export const jobsApi = {
  async getAll() {
    const { data } = await api.get<Job[]>('/jobs');
    return data;
  },
  async getById(id: string) {
    const { data } = await api.get<Job>(`/jobs/${id}`);
    return data;
  },
  async create(job: Omit<Job, 'id' | 'createdAt'>) {
    const { data } = await api.post<Job>('/jobs', job);
    return data;
  },
  async update(id: string, job: Partial<Omit<Job, 'id' | 'createdAt'>>) {
    const { data } = await api.put<Job>(`/jobs/${id}`, job);
    return data;
  },
  async delete(id: string) {
    await api.delete(`/jobs/${id}`);
  },
  async generateDescription(request: JobDescriptionRequest) {
    const payload: JobDescriptionRequest = {
      ...request,
      employmentType: request.employmentType || 'Full-time',
      requiredSkills: request.requiredSkills ?? [],
      preferredSkills: request.preferredSkills ?? [],
      tone: request.tone || 'Professional and inclusive',
    };

    const { data } = await api.post<{ description: string } | string>('/jobs/generate-description', payload);
    return typeof data === 'string' ? data : data.description;
  },
  async getCandidates(id: string) {
    const { data } = await api.get<MatchResult[]>(`/jobs/${id}/candidates`);
    return data;
  },
};

export const matchingApi = {
  async matchCandidateToJob(candidateId: string, jobId: string) {
    const { data } = await api.post<MatchResult>(`/matching/candidate/${candidateId}/job/${jobId}`);
    return data;
  },
  async matchCandidateToAllJobs(candidateId: string) {
    const { data } = await api.post<MatchResult[]>(`/matching/candidate/${candidateId}/all-jobs`);
    return data;
  },
  async matchJobToAllCandidates(jobId: string) {
    const { data } = await api.post<MatchResult[]>(`/matching/job/${jobId}/all-candidates`);
    return data;
  },
};

export default api;
