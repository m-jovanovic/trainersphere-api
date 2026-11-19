import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { queries } from '@/lib/api/queries';
import { ProgramEditorPage } from '@/features/programs/program-editor-page';

/** `new` opens an empty program. */
export const Route = createFileRoute('/_authed/_shell/_trainer/programs/$programId')({
  validateSearch: z.object({ assign: z.boolean().optional() }),
  loader: ({ context, params }) =>
    params.programId === 'new' ? undefined : context.queryClient.query({ ...queries.program(params.programId), staleTime: 'static' }),
  head: () => ({ meta: [{ title: 'Program · TrainerSphere' }] }),
  component: ProgramEditorPage,
});
