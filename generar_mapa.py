import os

# Carpetas que queremos ignorar para no ensuciar el mapa
IGNORAR = {'bin', 'obj', 'node_modules', '.git', '.vs', '__pycache__'}

def generar_arbol(ruta_base, archivo_salida):
    with open(archivo_salida, 'w', encoding='utf-8') as f:
        f.write(f"Estructura del Proyecto:\n")
        f.write("========================\n\n")
        
        for root, dirs, files in os.walk(ruta_base):
            # Filtramos las carpetas ignoradas
            dirs[:] = [d for d in dirs if d not in IGNORAR]
            
            # Calculamos el nivel de indentación
            level = root.replace(ruta_base, '').count(os.sep)
            indent = ' ' * 4 * (level)
            f.write(f"{indent}{os.path.basename(root)}/\n")
            subindent = ' ' * 4 * (level + 1)
            for file in files:
                f.write(f"{subindent}{file}\n")

if __name__ == "__main__":
    ruta_actual = os.getcwd()
    salida = os.path.join(ruta_actual, ".contexto", "mapa_proyecto.txt")
    generar_arbol(ruta_actual, salida)
    print(f"Mapa generado exitosamente en {salida}")