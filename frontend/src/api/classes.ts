import { apiFetch } from './client';
import type { ClassSummary, CreateClassRequest, CreatedClass } from './types';

export const listClasses = (signal?: AbortSignal) =>
  apiFetch<ClassSummary[]>('/classes', { signal });

export const getClass = (classId: string, signal?: AbortSignal) =>
  apiFetch<ClassSummary>(`/classes/${encodeURIComponent(classId)}`, { signal });

export const createClass = (request: CreateClassRequest) =>
  apiFetch<CreatedClass>('/classes', { method: 'POST', body: request });
