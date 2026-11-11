import { lazy } from "react";
import { createBrowserRouter } from "react-router-dom";
import { AdminLayout } from "@/features/admin/components/admin-layout";
import { StudentLayout } from "@/features/student/components/student-layout";
import { RouteErrorPage } from "@/components/layout/route-error-page";
import { SiteLayout } from "@/components/layout/site-layout";

const HomePage = lazy(() =>
  import("@/pages/home-page").then((module) => ({ default: module.HomePage })),
);

const NotFoundPage = lazy(() =>
  import("@/pages/not-found-page").then((module) => ({
    default: module.NotFoundPage,
  })),
);

const AdminDashboardPage = lazy(() =>
  import("@/features/admin/pages/admin-dashboard-page").then((module) => ({
    default: module.AdminDashboardPage,
  })),
);

const AdminClassesPage = lazy(() =>
  import("@/features/admin/pages/admin-classes-page").then((module) => ({
    default: module.AdminClassesPage,
  })),
);

const AdminCreateClassPage = lazy(() =>
  import("@/features/admin/pages/admin-create-class-page").then((module) => ({
    default: module.AdminCreateClassPage,
  })),
);

const AdminClassDetailPage = lazy(() =>
  import("@/features/admin/pages/admin-class-detail-page").then((module) => ({
    default: module.AdminClassDetailPage,
  })),
);

const StudentEntryPage = lazy(() =>
  import("@/features/student/pages/student-entry-page").then((module) => ({
    default: module.StudentEntryPage,
  })),
);

const StudentApplyPage = lazy(() =>
  import("@/features/student/pages/student-apply-page").then((module) => ({
    default: module.StudentApplyPage,
  })),
);

const StudentApplicationsPage = lazy(() =>
  import("@/features/student/pages/student-applications-page").then(
    (module) => ({
      default: module.StudentApplicationsPage,
    }),
  ),
);

const StudentHoldDetailPage = lazy(() =>
  import("@/features/student/pages/student-hold-detail-page").then((module) => ({
    default: module.StudentHoldDetailPage,
  })),
);

export const router = createBrowserRouter([
  {
    path: "/",
    element: <SiteLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      {
        index: true,
        element: <HomePage />,
      },
      {
        path: "admin",
        element: <AdminLayout />,
        children: [
          {
            index: true,
            element: <AdminDashboardPage />,
          },
          {
            path: "classes",
            element: <AdminClassesPage />,
          },
          {
            path: "classes/new",
            element: <AdminCreateClassPage />,
          },
          {
            path: "classes/:classId",
            element: <AdminClassDetailPage />,
          },
        ],
      },
      {
        path: "student",
        element: <StudentLayout />,
        children: [
          {
            index: true,
            element: <StudentEntryPage />,
          },
          {
            path: "apply",
            element: <StudentApplyPage />,
          },
          {
            path: "applications",
            element: <StudentApplicationsPage />,
          },
          {
            path: "applications/:holdId",
            element: <StudentHoldDetailPage />,
          },
        ],
      },
      {
        path: "*",
        element: <NotFoundPage />,
      },
    ],
  },
]);
