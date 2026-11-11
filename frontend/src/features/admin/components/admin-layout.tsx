import { NavLink, Outlet } from "react-router-dom";
import { cn } from "@/lib/utils";

const navItems = [
  { to: "/admin", label: "Overview", end: true },
  { to: "/admin/classes", label: "Classes", end: true },
  { to: "/admin/classes/new", label: "Create class", end: true },
];

export function AdminLayout() {
  return (
    <div>
      <div className="border-b border-slate-200 bg-white">
        <div className="site-container">
          <nav
            aria-label="Administration"
            className="flex gap-1 overflow-x-auto py-2"
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
        </div>
      </div>
      <Outlet />
    </div>
  );
}
