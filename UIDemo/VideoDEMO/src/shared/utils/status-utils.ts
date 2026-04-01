import type { VideoStatus } from '../../domain/models/video';

/**
 * Normalizes various status formats (numeric strings, enum names, numbers)
 * to the internal VideoStatus type used by the UI.
 * 
 * Also handles potential binding issues by checking both 'status' and 'Status' properties
 * if an object is passed.
 */
export const normalizeVideoStatus = (input: any): VideoStatus => {
  // Extract status from object if necessary (handles PascalCase vs camelCase binding issues)
  let status = input;
  if (input && typeof input === 'object') {
    status = input.status !== undefined ? input.status : input.Status;
  }

  if (status === undefined || status === null) return 'Processing';

  // Handle actual numbers
  if (typeof status === 'number') {
    switch (status) {
      case 0: return 'Processing';
      case 1: return 'Published';
      case 2: return 'Failed';
      case 3: return 'Deleted';
      default: return 'Processing';
    }
  }

  const raw = status.toString().trim();
  const lower = raw.toLowerCase();

  // Handle numeric strings (e.g. "1")
  if (lower === '0') return 'Processing';
  if (lower === '1') return 'Published';
  if (lower === '2') return 'Failed';
  if (lower === '3') return 'Deleted';

  // Handle enum names (e.g. "Published")
  switch (lower) {
    case 'processing': return 'Processing';
    case 'published':
    case 'live': 
      return 'Published';
    case 'failed': return 'Failed';
    case 'deleted': return 'Deleted';
    default: return 'Processing';
  }
};

export const isFinalState = (input: any): boolean => {
  const normalized = normalizeVideoStatus(input);
  return normalized === 'Published' || normalized === 'Failed' || normalized === 'Deleted';
};
