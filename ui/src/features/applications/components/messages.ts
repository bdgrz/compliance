import { ApplicationRequestError } from '../applications.js';

export function today(): string {
  return new Date().toISOString().slice(0, 10);
}

// Date inputs give a calendar day; the API takes an instant, so use the start of that UTC day.
export function startOfDay(date: string): string {
  return `${date}T00:00:00Z`;
}

// Maps a failed write to what the person can do next: reload after a stale revision, wait after
// projection lag, or ask for a waiver after a separation-of-duties conflict.
export function messageFor(error: Error, subject: string): string {
  if (!(error instanceof ApplicationRequestError)) return error.message;
  if (error.separationOfDuties) {
    return `Separation of duties: ${error.message} Ask an Org Admin for an exact-scope waiver, or have another Compliance Lead decide.`;
  }
  if (error.status === 403) return error.message;
  if (error.status === 409 && !error.transient) {
    return `Someone else changed ${subject} since you opened it. Reload to see their changes, then try again. (${error.message})`;
  }
  return error.message;
}
