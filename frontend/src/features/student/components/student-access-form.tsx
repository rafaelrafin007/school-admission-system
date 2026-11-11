import { useId } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button";
import { ArrowRightIcon, IdentificationIcon } from "@/components/ui/icons";
import { Input } from "@/components/ui/input";
import { useStudentIdentity } from "@/features/student/hooks/use-student-identity";
import {
  studentIdSchema,
  type StudentIdFormValues,
} from "@/features/student/schemas/student-schema";

interface StudentAccessFormProps {
  submitLabel?: string;
  to?: string;
  compact?: boolean;
}

export function StudentAccessForm({
  submitLabel = "Continue",
  to = "/student/apply",
  compact = false,
}: StudentAccessFormProps) {
  const inputId = useId();
  const errorId = useId();
  const helpId = useId();
  const navigate = useNavigate();
  const { studentId, setStudentId } = useStudentIdentity();
  const form = useForm<StudentIdFormValues>({
    resolver: zodResolver(studentIdSchema),
    defaultValues: {
      studentId: studentId ?? "",
    },
  });

  const {
    formState: { errors },
  } = form;

  const onSubmit = form.handleSubmit((values) => {
    setStudentId(values.studentId);
    navigate(to, { replace: true });
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-4">
      <div>
        <label
          htmlFor={inputId}
          className="block text-sm font-semibold text-slate-800"
        >
          Birth registration number
        </label>
        {!compact ? (
          <p id={helpId} className="mt-1 text-xs leading-5 text-slate-500">
            Enter the number exactly as registered. Leading zeros are kept, and
            up to 17 characters are supported.
          </p>
        ) : null}
        <div className="relative mt-2">
          <IdentificationIcon className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-500" />
          <Input
            id={inputId}
            className="pl-9"
            placeholder="e.g. 0198456712"
            maxLength={17}
            autoComplete="off"
            autoCapitalize="off"
            spellCheck={false}
            aria-invalid={Boolean(errors.studentId)}
            aria-describedby={
              errors.studentId
                ? `${compact ? "" : `${helpId} `}${errorId}`.trim()
                : compact
                  ? undefined
                  : helpId
            }
            {...form.register("studentId")}
          />
        </div>
        {errors.studentId ? (
          <p id={errorId} className="mt-1.5 text-sm text-red-700">
            {errors.studentId.message}
          </p>
        ) : null}
      </div>

      <Button type="submit" className="w-full sm:w-auto">
        {submitLabel}
        <ArrowRightIcon className="size-4" />
      </Button>
    </form>
  );
}
