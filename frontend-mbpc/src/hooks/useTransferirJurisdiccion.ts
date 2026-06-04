import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import viajesService from '../services/viajes.service';
import { viajesKeys } from './useViajes';

export interface TransferirJurisdiccionPayload {
  viajeId: string;
  nuevaCosteraId: number;
}

/**
 * Hook customizado de TanStack Query para la transferencia automática de jurisdicción de un viaje (Geofencing).
 * Realiza la llamada al endpoint PUT `/api/viajes/${id}/transferir` e invalida las queries de viajes
 * para lograr el aislamiento operativo multitenant.
 */
export function useTransferirJurisdiccion(): UseMutationResult<
  void,
  Error,
  TransferirJurisdiccionPayload
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ viajeId, nuevaCosteraId }: TransferirJurisdiccionPayload): Promise<void> => {
      await viajesService.transferir(viajeId, nuevaCosteraId);
    },
    onSuccess: () => {
      // Invalida la query clave 'viajes' para que se refresque el dashboard/mapa y se logre
      // el aislamiento operativo (el buque desaparece si el operador no tiene permisos en la nueva costera).
      queryClient.invalidateQueries({ queryKey: viajesKeys.all });
    },
  });
}
