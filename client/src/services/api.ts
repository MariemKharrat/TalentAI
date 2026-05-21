import axios from 'axios';
import { Candidate, Job, JobDescriptionRequest, MatchResult } from '../types';

const api = axios.create({
  baseURL: 'http://localhost:5000/api',
});

export const candidatesApi = {
  async uploadCv(file: File) {
    const formData = new FormData();
    formData.append('file', file);

    const { data } = await api.post<Candidate>('/candidates/upload-cv', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return data;
  },
  async getAll() {
    const { data } = await api.get<Candidate[]>('/candidates');
    return data;
  },
  async getById(id: string) {
    const { data } = await api.get<Candidate>(`/candidates/${id}`);
    return data;
  },
  async getMatches(id: string) {
    const { data } = await api.get<MatchResult[]>(`/candidates/${id}/matches`);
    return data;
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
    const { data } = await api.post<{ description: string } | string>('/jobs/generate-description', request);
    return typeof data === 'string' ? data : data.description;
  },
  async getCandidates(id: string) {
    const { data } = await api.get<MatchResult[]>(`/jobs/${id}/candidates`);
    return data;
  },
};

export const matchingApi = {
  async matchCandidateToJob(candidateId: string, jobId: string) {
    const { data } = await api.post<MatchResult>(`/matching/candidates/${candidateId}/jobs/${jobId}`);
    return data;
  },
  async matchCandidateToAllJobs(candidateId: string) {
    const { data } = await api.post<MatchResult[]>(`/matching/candidates/${candidateId}/all-jobs`);
    return data;
  },
  async matchJobToAllCandidates(jobId: string) {
    const { data } = await api.post<MatchResult[]>(`/matching/jobs/${jobId}/all-candidates`);
    return data;
  },
};

export default api;
