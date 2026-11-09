import { Route, Routes } from 'react-router';
import { Layout } from './components/Layout';
import { ApplicationPage } from './pages/ApplicationPage';
import { ClassListPage } from './pages/ClassListPage';
import { CreateClassPage } from './pages/CreateClassPage';
import { MyApplicationsPage } from './pages/MyApplicationsPage';
import { NotFoundPage } from './pages/NotFoundPage';

/** Route table (Architecture.md §11). nginx falls back to index.html, so deep links survive refresh. */
export function AppRoutes() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<ClassListPage />} />
        <Route path="admin/classes/new" element={<CreateClassPage />} />
        <Route path="applications/:holdId" element={<ApplicationPage />} />
        <Route path="my-applications" element={<MyApplicationsPage />} />
        <Route path="*" element={<NotFoundPage />} />
      </Route>
    </Routes>
  );
}
