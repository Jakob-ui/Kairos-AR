# AR Picture Place

## Projektbeschreibung
Dieses Unity-Projekt wurde im Rahmen einer Bachelorarbeit entwickelt und dient als Prototyp zur Untersuchung von AR-Lokalisierungstechniken im Outdoor-Bereich. Die App verwendet die Geospatial-API von ARCore, um Objekte basierend auf GPS-Koordinaten zu platzieren und deren Genauigkeit zu analysieren.

---

## Hauptfunktionen
- **Geospatial Object Placement**: Objekte werden basierend auf Latitude, Longitude und Altitude platziert.
- **Datenanalyse**: Erfassung von Positionsabweichungen und Rotationsdifferenzen.
- **Firebase-Integration**: Speicherung von Positions- und Rotationsdaten in Firestore.

---

## Voraussetzungen
- **Unity**: Version `2022.3.29f1` oder kompatibel.
- **Android SDK** und **NDK**: Für Android-Builds.
- **Xcode**: Für iOS-Builds.
- **Firebase Console**: Für die Firebase-Integration.
- Ein Gerät mit **ARCore**- oder **ARKit**-Unterstützung.

---

## Einrichtung

### 1. Unity-Projekt vorbereiten
1. Erstelle ein neues Unity-Projekt (3D Core oder URP).
2. Stelle die Plattform auf **Android** oder **iOS**:
   - **File > Build Settings > Switch Platform**.

### 2. ARCore Extensions installieren
1. Öffne den **Unity Package Manager**:
   - **Window > Package Manager**.
2. Füge das Paket hinzu:
   - **com.google.ar.core.arfoundation.extensions**

3. Aktiviere ARCore/ARKit:
   - **Edit > Project Settings > XR Plug-in Management**.

### 3. Firebase einrichten
1. Erstelle ein Firebase-Projekt und füge eine Android- und/oder iOS-App hinzu.
2. Lade die Konfigurationsdateien herunter:
- **`google-services.json`** (für Android).
- **`GoogleService-Info.plist`** (für iOS).
3. Platziere die Dateien in **`Assets/StreamingAssets`**.
4. Installiere das Firebase Unity SDK:
https://github.com/firebase/firebase-unity-sdk.git


### 4. Android-spezifische Einstellungen
Minimale API-Version: Android 7.0 (API Level 24).
