import { Link } from "react-router-dom";
import { ClassAvailabilityBadge } from "@/components/shared/class-availability";
import { ChevronRightIcon } from "@/components/ui/icons";
import { formatNumber } from "@/lib/utils";
import type { ClassResponse } from "@/types/api";

export function ClassTable({ classes }: { classes: ClassResponse[] }) {
  return (
    <>
      <p className="mb-2 text-xs text-slate-500 md:hidden">
        Scroll sideways to see every column.
      </p>
      <div
        role="region"
        aria-label="Admission classes table"
        tabIndex={0}
        className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2"
      >
        <table className="min-w-[680px] w-full border-collapse text-left text-sm">
          <caption className="sr-only">
            Admission classes with seat capacity and availability
          </caption>
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-xs font-semibold text-slate-500">
              <th scope="col" className="px-4 py-3 font-semibold">
                Class
              </th>
              <th scope="col" className="px-4 py-3 font-semibold text-right">
                Seats
              </th>
              <th scope="col" className="px-4 py-3 font-semibold text-right">
                Held
              </th>
              <th scope="col" className="px-4 py-3 font-semibold text-right">
                Confirmed
              </th>
              <th scope="col" className="px-4 py-3 font-semibold text-right">
                Available
              </th>
              <th scope="col" className="px-4 py-3 font-semibold">
                Status
              </th>
              <th scope="col" className="px-4 py-3">
                <span className="sr-only">Open</span>
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {classes.map((classData) => (
              <tr key={classData.id} className="hover:bg-slate-50/80">
                <th scope="row" className="px-4 py-3.5 font-medium">
                  <Link
                    to={`/admin/classes/${encodeURIComponent(classData.id)}`}
                    className="text-slate-900 hover:text-brand-800"
                  >
                    {classData.name}
                  </Link>
                </th>
                <td className="px-4 py-3.5 text-right tabular-nums text-slate-700">
                  {formatNumber(classData.totalSeats)}
                </td>
                <td className="px-4 py-3.5 text-right tabular-nums text-slate-700">
                  {formatNumber(classData.heldSeats)}
                </td>
                <td className="px-4 py-3.5 text-right tabular-nums text-slate-700">
                  {formatNumber(classData.confirmedSeats)}
                </td>
                <td className="px-4 py-3.5 text-right font-semibold tabular-nums text-slate-900">
                  {formatNumber(classData.availableSeats)}
                </td>
                <td className="px-4 py-3.5">
                  <ClassAvailabilityBadge
                    availableSeats={classData.availableSeats}
                  />
                </td>
                <td className="px-4 py-3.5 text-right">
                  <Link
                    to={`/admin/classes/${encodeURIComponent(classData.id)}`}
                    className="inline-flex items-center gap-1 font-semibold text-brand-800 hover:text-brand-900"
                  >
                    View
                    <ChevronRightIcon className="size-4" />
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
