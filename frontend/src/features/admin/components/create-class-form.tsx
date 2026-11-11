import { useState } from "react";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { getApiErrorMessage, getApiFieldErrors } from "@/lib/api/client";
import { useCreateClass } from "@/features/admin/hooks/use-create-class";
import {
  classFormSchema,
  type ClassFormInput,
  type ClassFormValues,
} from "@/features/admin/schemas/class-schema";
import type { CreateClassResponse } from "@/types/api";

export function CreateClassForm() {
  const mutation = useCreateClass();
  const [createdClass, setCreatedClass] = useState<CreateClassResponse | null>(
    null,
  );
  const form = useForm<ClassFormInput, unknown, ClassFormValues>({
    resolver: zodResolver(classFormSchema),
    defaultValues: {
      name: "",
      totalSeats: "",
    },
  });

  const {
    formState: { errors },
  } = form;

  const onSubmit = form.handleSubmit((values) => {
    setCreatedClass(null);
    mutation.mutate(values, {
      onSuccess: (created) => {
        setCreatedClass(created);
        form.reset();
      },
      onError: (error) => {
        const fieldErrors = getApiFieldErrors(error);
        if (!fieldErrors) return;

        for (const [field, messages] of Object.entries(fieldErrors)) {
          const message = messages.join(" ");
          if (field.toLowerCase() === "name") {
            form.setError("name", { type: "server", message });
          }
          if (field.toLowerCase() === "totalseats") {
            form.setError("totalSeats", { type: "server", message });
          }
        }
      },
    });
  });

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      {mutation.isError ? (
        <Alert tone="danger" title="Class could not be created">
          {getApiErrorMessage(mutation.error)}
        </Alert>
      ) : null}

      {createdClass ? (
        <Alert tone="success" title="Class created">
          <p>
            {createdClass.name} is now open with{" "}
            {createdClass.totalSeats.toLocaleString()} seats.
          </p>
          <Link
            to={`/admin/classes/${encodeURIComponent(createdClass.id)}`}
            className="mt-1 inline-block font-semibold underline underline-offset-2"
          >
            View class details
          </Link>
        </Alert>
      ) : null}

      <div>
        <label
          htmlFor="class-name"
          className="block text-sm font-semibold text-slate-800"
        >
          Class name
        </label>
        <p id="class-name-help" className="mt-1 text-xs text-slate-500">
          Use a clear name families will recognise, such as “Class 6 – Morning”.
        </p>
        <Input
          id="class-name"
          className="mt-2"
          placeholder="Class 6 – Morning"
          maxLength={160}
          autoComplete="off"
          aria-invalid={Boolean(errors.name)}
          aria-describedby={
            errors.name ? "class-name-help class-name-error" : "class-name-help"
          }
          {...form.register("name")}
        />
        {errors.name ? (
          <p id="class-name-error" className="mt-1.5 text-sm text-red-700">
            {errors.name.message}
          </p>
        ) : null}
      </div>

      <div>
        <label
          htmlFor="class-seats"
          className="block text-sm font-semibold text-slate-800"
        >
          Total seats
        </label>
        <p id="class-seats-help" className="mt-1 text-xs text-slate-500">
          The maximum number of confirmed admissions for this class.
        </p>
        <Input
          id="class-seats"
          type="number"
          min={1}
          step={1}
          inputMode="numeric"
          className="mt-2 max-w-xs"
          placeholder="40"
          aria-invalid={Boolean(errors.totalSeats)}
          aria-describedby={
            errors.totalSeats
              ? "class-seats-help class-seats-error"
              : "class-seats-help"
          }
          {...form.register("totalSeats")}
        />
        {errors.totalSeats ? (
          <p id="class-seats-error" className="mt-1.5 text-sm text-red-700">
            {errors.totalSeats.message}
          </p>
        ) : null}
      </div>

      <Button type="submit" loading={mutation.isPending}>
        Create class
      </Button>
    </form>
  );
}
