export interface WorkExperience {
  id: string;
  company: string;
  title: string;
  startDate: string;
  endDate?: string | null;
  description: string;
}

export interface Education {
  id: string;
  institution: string;
  degree: string;
  fieldOfStudy: string;
  startDate: string;
  endDate?: string | null;
}

export type CvParsingMethod = 'DocumentIntelligence' | 'ContentUnderstanding';

export interface Candidate {
  id: string;
  firstName: string;
  lastName: string;
  fullName?: string;
  email: string;
  phone: string;
  skills: string[];
  experience: WorkExperience[];
  education: Education[];
  summary: string;
  cvFileUrl: string;
  cvFileName?: string;
  createdAt: string;
  createdAtUtc?: string;
  parsingMethod?: string;
}

export interface Job {
  id: string;
  title: string;
  description: string;
  department: string;
  requiredSkills: string[];
  preferredSkills: string[];
  experienceLevel: string;
  location: string;
  isActive: boolean;
  createdAt: string;
}

export enum MatchLevel {
  High = 'High',
  Medium = 'Medium',
  Low = 'Low',
}

export interface MatchResult {
  id: string;
  candidateId: string;
  jobId: string;
  score: number;
  matchLevel: MatchLevel;
  skillMatches: string[];
  skillGaps: string[];
  explanation: string;
  createdAt: string;
}

export interface JobDescriptionRequest {
  title: string;
  department: string;
  requiredSkills: string[];
  experienceLevel: string;
  policyContext: string;
}
