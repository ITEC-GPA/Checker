namespace GPC.Design.Bridges;
public static partial class BridgeConcept
{
    private static void CostWarnings(List<string> warnings, double length, double bearingForce, double bearingCount, double bearingRate)
    {
        double average = bearingForce / Math.Max(1, bearingCount);
        if (average > 1500 || bearingRate < 2500) warnings.Add($"Appoggi: carico medio equivalente {average:0} kN/dispositivo, prezzo {bearingRate:0} €/cad. Verificare portata e corsa: il listino ANAS usa €/kN; nessun dimensionamento dei dispositivi.");
        if (length > 200) warnings.Add("Giunti: il prezzo fisso non segue corsa termica/sismica e lunghezza dilatabile. Per grandi opere può sottostimare il costo.");
        warnings.Add("Computo parziale: scavi, rinterri, drenaggi, protezioni, impermeabilizzazione, centine alte, accessi e sicurezza specifica non computati analiticamente. Oneri aggiuntivi e imprevisti sono riserve, non un computo completo.");
    }
}
