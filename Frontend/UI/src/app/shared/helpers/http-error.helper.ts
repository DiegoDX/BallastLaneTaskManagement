import { HttpErrorResponse } from '@angular/common/http';

export function resolveHttpErrorMessage(
  error: HttpErrorResponse,
  fallbackMessage: string,
): string {
  if (error.status === 0) {
    return 'Server not available';
  }

  if (error.status === 404) {
    return 'Task not found';
  }

  if (error.status === 400) {
    const apiError = error.error as { message?: string } | null;
    return apiError?.message ?? 'Validation failed. Please check your input.';
  }

  if (error.status === 502) {
    return 'AI service returned an invalid response. Please try again.';
  }

  if (error.status === 503) {
    return 'AI service is temporarily unavailable. Please try again later.';
  }

  if (error.status >= 500) {
    return 'An unexpected error occurred. Please try again.';
  }

  return fallbackMessage;
}
