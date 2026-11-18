import manifest from './exercises.json';
import type { Exercise, Taxonomy } from '@/lib/api/types';

type SeedExercise = Omit<Exercise, 'id' | 'isCustom' | 'archived'>;

// Library and vocabulary from the illustration set's manifest (exercises/exercises.json), which the API seeds from too.
export const seedTaxonomy: Taxonomy = manifest.taxonomy;
export const seedExercises: SeedExercise[] = manifest.exercises;
