import { ApiError } from '../api/client';

/**
 * Every user-facing string lives here (UI text is English; centralised for easy translation),
 * including the error-code → message mapping (Design.md §5).
 */
export const messages = {
  app: {
    title: 'School Admission',
  },
  nav: {
    label: 'Main',
    classes: 'Classes',
    myApplications: 'My Applications',
    createClass: 'Admin: Create Class',
  },
  studentId: {
    label: 'Birth Registration No.',
    placeholder: '17-digit birth registration number',
    helper: "Enter the student's birth registration number to apply or see applications.",
    saved: '✓ Saved',
    savedHint: 'Saved on this device. Applications are made for this number.',
    change: 'Change',
    digitsOnly: 'Use digits only (0–9 or ০–৯).',
    wrongLength: (expected: number, entered: number) =>
      `Must be exactly ${expected} digits (you entered ${entered}).`,
  },
  pages: {
    classes: 'Classes',
    createClass: 'Create a class',
    application: 'Application',
    myApplications: 'My applications',
  },
  classList: {
    caption: 'Classes and seats',
    loading: 'Loading classes…',
    empty: 'No classes yet. An admin can create one from',
    emptyLink: 'Admin: Create Class',
    columns: { name: 'Class', total: 'Total', held: 'Held', confirmed: 'Confirmed', available: 'Available', action: 'Action' },
    updatedJustNow: 'Updated just now',
    updatedAgo: (seconds: number) => `Updated ${seconds} s ago`,
    autoRefresh: 'Seat numbers refresh automatically every 3 seconds.',
    needStudentId: 'Enter a birth registration number at the top of the page to apply.',
    apply: 'Apply',
    applyFor: (className: string) => `Apply for ${className}`,
    applying: 'Applying…',
    full: 'Full',
    viewApplication: 'View application',
    admitted: 'Admitted ✓',
    retry: 'Retry',
  },
  createClass: {
    intro: 'Each shift is its own class, e.g. "Class 6 - Morning Shift".',
    nameLabel: 'Class name',
    namePlaceholder: 'Class 6 - Morning Shift',
    seatsLabel: 'Total seats',
    submit: 'Create class',
    submitting: 'Creating…',
    created: (name: string, seats: number) => `✓ "${name}" created with ${seats} seats.`,
    viewClasses: 'View classes',
    nameRequired: 'Name is required.',
    nameTooLong: (max: number) => `Name must be at most ${max} characters.`,
    seatsRequired: 'Total seats is required.',
    seatsWhole: 'Total seats must be a whole number.',
    seatsRange: (min: number, max: number) => `Total seats must be between ${min} and ${max}.`,
  },
  application: {
    titleFor: (className: string) => `Application — ${className}`,
    loading: 'Loading application…',
    statusLabel: 'Status',
    timeLeftLabel: 'Time left',
    expiresAt: (time: string) => `(expires at ${time})`,
    hurry: 'Hurry!',
    announceOneMinute: 'One minute left to pay.',
    announceExpired: 'Time ran out. The seat was released.',
    status: {
      HELD: 'Seat held — pay before the timer ends.',
      CONFIRMED: 'Admission confirmed. 🎉',
      EXPIRED: 'Time ran out. The seat was released. You can apply again if seats are available.',
      CANCELLED: 'You cancelled this application.',
    },
    pay: 'Pay Admission Fee',
    paying: 'Paying…',
    cancel: 'Cancel application',
    cancelling: 'Cancelling…',
    simulated: 'ℹ Paying is simulated in this demo — clicking the button counts as a successful payment.',
    payDisabledExpired: 'This application has expired, so it can no longer be paid.',
    confirmTitle: 'Cancel this application?',
    confirmBody: 'Your seat will be released immediately.',
    confirmYes: 'Yes, cancel it',
    confirmNo: 'Keep my application',
    paid: '✓ Payment received. The admission is confirmed.',
    cancelled: 'Your application was cancelled and the seat was released.',
    notOwned: 'This application belongs to a different birth registration number.',
    needStudentId: 'Enter the birth registration number this application was made for at the top of the page to pay or cancel.',
    backToClasses: 'Back to classes',
    myApplications: 'My applications',
  },
  myApplications: {
    titleFor: (studentId: string) => `My applications (${studentId})`,
    caption: 'Applications for this birth registration number',
    needStudentId: "Enter the student's birth registration number at the top of the page to see applications.",
    loading: 'Loading applications…',
    empty: 'No applications yet. Go to',
    emptyLink: 'Classes',
    emptyAfter: 'to apply.',
    columns: { name: 'Class', status: 'Status', timeLeft: 'Time left', action: 'Action' },
    none: '—',
    open: 'Open',
    openFor: (className: string) => `Open application for ${className}`,
    autoRefresh: 'This list refreshes automatically every 5 seconds.',
  },
  errors: {
    NO_SEATS_AVAILABLE:
      "Sorry, all seats in this class are taken right now. Seats may open up if other applicants don't pay in time.",
    ACTIVE_HOLD_EXISTS: 'You already have an active application for this class.',
    ALREADY_ADMITTED: 'This student is already admitted to this class.',
    HOLD_EXPIRED:
      "This application has expired, so it can't be paid or cancelled. You can apply again if seats are available.",
    HOLD_CANCELLED: "This application was cancelled and can't be paid.",
    HOLD_ALREADY_CONFIRMED: "This admission is already confirmed and can't be cancelled here.",
    HOLD_NOT_OWNED:
      'This application belongs to a different birth registration number. Check the number at the top of the page.',
    INVALID_STUDENT_ID: 'Please enter a valid 17-digit birth registration number at the top of the page.',
    CLASS_NAME_EXISTS: 'A class with this name already exists. Use a different name (e.g., add the shift).',
    VALIDATION_FAILED: 'Some information is missing or invalid. Please check the form.',
    MALFORMED_JSON: 'Something was wrong with the information sent. Please try again.',
    CLASS_NOT_FOUND: 'This class no longer exists.',
    HOLD_NOT_FOUND: "We couldn't find this application. It may have been entered incorrectly.",
    NOT_FOUND: "We couldn't find this application. It may have been entered incorrectly.",
    SERVICE_UNAVAILABLE: 'The server is having trouble right now. Please try again in a moment.',
    server: 'The server is having trouble right now. Please try again in a moment.',
    network: "Can't reach the server. Check your internet connection — we'll keep retrying.",
    timeout: 'The server is taking too long to respond. Please try again.',
    unknown: 'Something went wrong. Please try again.',
    openIt: 'Open it',
    view: 'View',
  },
  notFound: {
    title: 'Page not found',
    body: "This page doesn't exist. It may have been moved or the link may be wrong.",
    back: 'Go to classes',
  },
  errorBoundary: {
    title: 'Something went wrong on this page.',
    body: 'Please reload the page. If it keeps happening, go back to the class list.',
    reload: 'Reload',
    home: 'Go to classes',
  },
} as const;

type ErrorCode = keyof typeof messages.errors;

/**
 * Any thrown value → a sentence a parent understands (R12). Never shows raw codes or stack traces;
 * unexpected errors are also logged for developers.
 */
export function errorMessage(error: unknown): string {
  const t = messages.errors;
  if (error instanceof ApiError) {
    if (error.kind === 'network') return t.network;
    if (error.kind === 'timeout') return t.timeout;
    if (error.code in t) return t[error.code as ErrorCode];
    if (error.status !== null && error.status >= 500) return t.server;
  }
  console.error('Unexpected error', error);
  return t.unknown;
}
