import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { IdentificationIcon } from "@/components/ui/icons";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/student", label: "Start", end: true },
  { to: "/student/apply", label: "Apply for a seat", end: true },
  {
    to: "/student/applications",
    label: "My applications",
    end: false,
  },
];

export function StudentLayout() {
  const { studentId, clearStudentIdentity } = useStudentIdentity();
  const navigate = useNavigate();

  function handleChangeStudent() {
    clearStudentIdentity();
    navigate("/student");
  }

  return (
    <div>
      <div className="border-b border-slate-200 bg-white">
        <div className="site-container flex flex-col gap-2 py-2 lg:flex-row lg:items-center lg:justify-between">
          <nav
            aria-label="Student portal"
            className="flex gap-1 overflow-x-auto"
          >
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  cn(
                    "whitespace-nowrap rounded-md px-3 py-2 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900",
                    isActive && "bg-brand-50 text-brand-800 hover:bg-brand-50",
                  )
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>

          {studentId ? (
            <div className="flex flex-wrap items-center gap-2 px-1 pb-1 text-xs text-slate-500 lg:pb-0">
              <span className="flex items-center gap-1.5">
                <IdentificationIcon className="size-4 text-slate-500" />
                Student
                <span className="text-sm font-semibold text-slate-800">
                  {studentId}
                </span>
              </span>
              <button
                type="button"
                onClick={handleChangeStudent}
                className="font-semibold text-brand-800 underline-offset-2 hover:underline"
              >
                Change
              </button>
            </div>
          ) : (
            <span className="px-1 pb-1 text-xs text-slate-500 lg:pb-0">
              Enter your birth registration number to begin
            </span>
          )}
        </div>
      </div>
      <Outlet />
    </div>
  );
}
