import { useQuery } from '@tanstack/react-query';
import catalogosService from '../services/catalogos.service';
import type { CatalogoDto } from '../types/catalogos.types';

export function useMuelles() {
  return useQuery<CatalogoDto[], Error>({
    queryKey: ['catalogos', 'muelles'],
    queryFn: catalogosService.getMuelles,
    staleTime: 1000 * 60 * 60, // 1 hora
  });
}

export function usePuntosControl() {
  return useQuery<CatalogoDto[], Error>({
    queryKey: ['catalogos', 'puntos-control'],
    queryFn: catalogosService.getPuntosControl,
    staleTime: 1000 * 60 * 60, // 1 hora
  });
}
