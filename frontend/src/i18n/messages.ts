/**
 * Every user-facing string lives here (UI text is English; centralised for easy translation).
 * Error-code → message mappings are added with the screens that need them (Design.md §5).
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
  pages: {
    classes: 'Classes',
    createClass: 'Create a class',
    application: 'Application',
    myApplications: 'My applications',
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
