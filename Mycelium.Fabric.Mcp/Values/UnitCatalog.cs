// ------------------------------------------------------------------------------------------------
//  <copyright file="UnitCatalog.cs" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

namespace Mycelium.Fabric.Mcp.Values
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// The units that the server knows, found from their symbol (<c>kg</c>, <c>km/h</c>, <c>m/s²</c>, <c>kWh</c>) or their
    /// name (<c>kilogram</c>): the SI units with their prefixes, the units accepted with the SI (minute, hour, degree, litre,
    /// tonne...), information units (bit, byte) and the usual imperial units (pound, foot, mile...). It replaces the SI and
    /// ISQ libraries of SysML v2, which the model does not contain.
    /// </summary>
    public static class UnitCatalog
    {
        /// <summary>
        /// The SI prefixes, longest first so that <c>da</c> is tried before <c>d</c>.
        /// </summary>
        private static readonly (string Symbol, string Name, double Factor)[] Prefixes =
        [
            ("da", "deca", 1e1), ("Q", "quetta", 1e30), ("R", "ronna", 1e27), ("Y", "yotta", 1e24), ("Z", "zetta", 1e21), ("E", "exa", 1e18), ("P", "peta", 1e15),
            ("T", "tera", 1e12), ("G", "giga", 1e9), ("M", "mega", 1e6), ("k", "kilo", 1e3), ("h", "hecto", 1e2), ("d", "deci", 1e-1), ("c", "centi", 1e-2),
            ("m", "milli", 1e-3), ("µ", "micro", 1e-6), ("μ", "micro", 1e-6), ("u", "micro", 1e-6), ("n", "nano", 1e-9), ("p", "pico", 1e-12), ("f", "femto", 1e-15),
            ("a", "atto", 1e-18)
        ];

        /// <summary>
        /// The superscript characters of an exponent, such as <c>²</c> in <c>m²</c>, with the character they stand for.
        /// </summary>
        private static readonly Dictionary<char, char> Superscripts = new()
        {
            ['⁻'] = '-', ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4', ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9'
        };

        /// <summary>
        /// The known units by symbol, with whether they accept a prefix.
        /// </summary>
        private static readonly Dictionary<string, (Unit Unit, bool IsPrefixable)> UnitsBySymbol = CreateUnits();

        /// <summary>
        /// The symbols of the known units by name, in lower case, for example <c>kilogram</c> for <c>kg</c>.
        /// </summary>
        private static readonly Dictionary<string, string> SymbolsByName = CreateNames();

        /// <summary>
        /// Finds a unit from its symbol or its name, simple or compound.
        /// </summary>
        /// <param name="text">The symbol or name, for example <c>kg</c>, <c>km/h</c>, <c>m/s^2</c>, <c>W*h</c> or <c>kilogram</c>.</param>
        /// <param name="unit">The unit, or <c>null</c> when it is not known.</param>
        /// <returns><c>true</c> when the unit is known.</returns>
        public static bool TryParse(string text, out Unit unit)
        {
            unit = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmedText = text.Trim();

            if (TryParseSimple(trimmedText, out unit))
            {
                return true;
            }

            var normalizedText = trimmedText.Replace('·', '*').Replace('⋅', '*');
            var position = 0;

            return TryParseProduct(normalizedText, ref position, out unit) && position == normalizedText.Length;
        }

        /// <summary>
        /// Parses a product or quotient of units, such as <c>kg*m/s^2</c>.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="position">The position of the first character to parse, moved after the parsed text.</param>
        /// <param name="unit">The parsed unit.</param>
        /// <returns><c>true</c> when the text is a unit.</returns>
        private static bool TryParseProduct(string text, ref int position, out Unit unit)
        {
            if (!TryParsePower(text, ref position, out unit))
            {
                return false;
            }

            while (position < text.Length && text[position] is '*' or '/')
            {
                var isDivision = text[position] == '/';
                position++;

                if (!TryParsePower(text, ref position, out var nextUnit))
                {
                    return false;
                }

                unit = isDivision ? unit.Divide(nextUnit) : unit.Multiply(nextUnit);
            }

            return true;
        }

        /// <summary>
        /// Parses a unit with an optional exponent, such as <c>m^2</c>, <c>m²</c>, <c>s⁻¹</c> or <c>(m/s)^2</c>.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="position">The position of the first character to parse, moved after the parsed text.</param>
        /// <param name="unit">The parsed unit.</param>
        /// <returns><c>true</c> when the text is a unit.</returns>
        private static bool TryParsePower(string text, ref int position, out Unit unit)
        {
            unit = null;

            if (position < text.Length && text[position] == '(')
            {
                position++;

                if (!TryParseProduct(text, ref position, out unit) || position >= text.Length || text[position] != ')')
                {
                    return false;
                }

                position++;
            }
            else
            {
                var start = position;

                while (position < text.Length && text[position] is not ('*' or '/' or '^' or '(' or ')') && !Superscripts.ContainsKey(text[position]))
                {
                    position++;
                }

                if (!TryParseSimple(text[start..position], out unit))
                {
                    return false;
                }
            }

            var exponentText = ReadExponent(text, ref position);

            if (exponentText == null)
            {
                return true;
            }

            if (!int.TryParse(exponentText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
            {
                return false;
            }

            unit = unit.Power(exponent);

            return true;
        }

        /// <summary>
        /// Reads the exponent that follows a unit, written <c>^2</c>, <c>^-1</c> or with superscripts (<c>²</c>, <c>⁻¹</c>).
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="position">The position of the exponent, moved after it.</param>
        /// <returns>The exponent as text, for example <c>-1</c>, or <c>null</c> when there is none.</returns>
        private static string ReadExponent(string text, ref int position)
        {
            if (position < text.Length && text[position] == '^')
            {
                var start = ++position;

                while (position < text.Length && (char.IsDigit(text[position]) || (position == start && text[position] is '-' or '+')))
                {
                    position++;
                }

                return text[start..position];
            }

            var superscriptStart = position;

            while (position < text.Length && Superscripts.ContainsKey(text[position]))
            {
                position++;
            }

            return position == superscriptStart ? null : string.Concat(text[superscriptStart..position].Select(character => Superscripts[character]));
        }

        /// <summary>
        /// Finds a simple unit: a known symbol (<c>kg</c> is <c>k</c> + <c>g</c>), or a known name (<c>kilogram</c>).
        /// </summary>
        /// <param name="text">The symbol or name.</param>
        /// <param name="unit">The unit, or <c>null</c>.</param>
        /// <returns><c>true</c> when the unit is known.</returns>
        private static bool TryParseSimple(string text, out Unit unit)
        {
            unit = FindBySymbol(text) ?? FindByName(text);

            return unit != null;
        }

        /// <summary>
        /// Finds a unit from its symbol, with or without a prefix.
        /// </summary>
        /// <param name="symbol">The symbol, for example <c>kWh</c>.</param>
        /// <returns>The unit, or <c>null</c>.</returns>
        private static Unit FindBySymbol(string symbol)
        {
            if (UnitsBySymbol.TryGetValue(symbol, out var known))
            {
                return known.Unit;
            }

            return Prefixes
                .Where(prefix => symbol.Length > prefix.Symbol.Length && symbol.StartsWith(prefix.Symbol, StringComparison.Ordinal))
                .Select(prefix => (prefix.Factor, Base: UnitsBySymbol.GetValueOrDefault(symbol[prefix.Symbol.Length..])))
                .Where(candidate => candidate.Base.Unit != null && candidate.Base.IsPrefixable)
                .Where(candidate => candidate.Base.Unit.Dimension.Information == 0 || candidate.Factor > 1)
                .Select(candidate => candidate.Base.Unit with { Symbol = symbol, Factor = candidate.Base.Unit.Factor * candidate.Factor })
                .FirstOrDefault();
        }

        /// <summary>
        /// Finds a unit from its name, with or without a prefix, ignoring the case.
        /// </summary>
        /// <param name="name">The name, for example <c>kilometre</c>.</param>
        /// <returns>The unit, or <c>null</c>.</returns>
        private static Unit FindByName(string name)
        {
            var lowerName = name.ToLowerInvariant();

            if (SymbolsByName.TryGetValue(lowerName, out var symbol))
            {
                return UnitsBySymbol[symbol].Unit with { Symbol = name };
            }

            return Prefixes
                .Where(prefix => lowerName.Length > prefix.Name.Length && lowerName.StartsWith(prefix.Name, StringComparison.Ordinal))
                .Select(prefix => (prefix.Factor, Symbol: SymbolsByName.GetValueOrDefault(lowerName[prefix.Name.Length..])))
                .Where(candidate => candidate.Symbol != null && UnitsBySymbol[candidate.Symbol].IsPrefixable)
                .Select(candidate => UnitsBySymbol[candidate.Symbol].Unit with { Symbol = name, Factor = UnitsBySymbol[candidate.Symbol].Unit.Factor * candidate.Factor })
                .FirstOrDefault();
        }

        /// <summary>
        /// Creates the known units.
        /// </summary>
        /// <returns>The units by symbol, with whether they accept a prefix.</returns>
        private static Dictionary<string, (Unit Unit, bool IsPrefixable)> CreateUnits()
        {
            var length = new Dimension { Length = 1 };
            var mass = new Dimension { Mass = 1 };
            var time = new Dimension { Time = 1 };
            var current = new Dimension { Current = 1 };
            var temperature = new Dimension { Temperature = 1 };
            var angle = new Dimension { Angle = 1 };
            var information = new Dimension { Information = 1 };
            var energy = new Dimension { Length = 2, Mass = 1, Time = -2 };
            var power = new Dimension { Length = 2, Mass = 1, Time = -3 };
            var pressure = new Dimension { Length = -1, Mass = 1, Time = -2 };
            var charge = new Dimension { Time = 1, Current = 1 };

            (string Symbol, Dimension Dimension, double Factor, double Offset, bool IsPrefixable)[] units =
            [
                ("m", length, 1, 0, true), ("g", mass, 1e-3, 0, true), ("s", time, 1, 0, true), ("A", current, 1, 0, true), ("K", temperature, 1, 0, true),
                ("mol", new Dimension { Amount = 1 }, 1, 0, true), ("cd", new Dimension { Luminosity = 1 }, 1, 0, true), ("rad", angle, 1, 0, true),
                ("sr", angle.Power(2), 1, 0, true), ("bit", information, 1, 0, true), ("B", information, 8, 0, true), ("bps", information * time.Power(-1), 1, 0, true),
                ("Hz", time.Power(-1), 1, 0, true), ("N", new Dimension { Length = 1, Mass = 1, Time = -2 }, 1, 0, true), ("Pa", pressure, 1, 0, true),
                ("J", energy, 1, 0, true), ("Wh", energy, 3600, 0, true), ("eV", energy, 1.602176634e-19, 0, true), ("W", power, 1, 0, true), ("C", charge, 1, 0, true),
                ("Ah", charge, 3600, 0, true), ("V", power * current.Power(-1), 1, 0, true), ("F", charge * charge * energy.Power(-1), 1, 0, true),
                ("Ω", power * current.Power(-2), 1, 0, true), ("ohm", power * current.Power(-2), 1, 0, true), ("S", power.Power(-1) * current.Power(2), 1, 0, true),
                ("Wb", energy * current.Power(-1), 1, 0, true), ("T", mass * time.Power(-2) * current.Power(-1), 1, 0, true),
                ("H", energy * current.Power(-2), 1, 0, true), ("lm", new Dimension { Luminosity = 1, Angle = 2 }, 1, 0, true),
                ("lx", new Dimension { Length = -2, Luminosity = 1, Angle = 2 }, 1, 0, true), ("Bq", time.Power(-1), 1, 0, true),
                ("Gy", new Dimension { Length = 2, Time = -2 }, 1, 0, true), ("Sv", new Dimension { Length = 2, Time = -2 }, 1, 0, true),
                ("kat", new Dimension { Amount = 1, Time = -1 }, 1, 0, true), ("L", length.Power(3), 1e-3, 0, true), ("l", length.Power(3), 1e-3, 0, true),
                ("t", mass, 1e3, 0, true), ("bar", pressure, 1e5, 0, true), ("as", angle, Math.PI / 648000, 0, true),
                ("°C", temperature, 1, 273.15, false), ("degC", temperature, 1, 273.15, false), ("°F", temperature, 5.0 / 9, 273.15 - 32 * 5.0 / 9, false),
                ("degF", temperature, 5.0 / 9, 273.15 - 32 * 5.0 / 9, false), ("min", time, 60, 0, false), ("h", time, 3600, 0, false), ("d", time, 86400, 0, false),
                ("yr", time, 31557600, 0, false), ("deg", angle, Math.PI / 180, 0, false), ("°", angle, Math.PI / 180, 0, false), ("arcmin", angle, Math.PI / 10800, 0, false),
                ("′", angle, Math.PI / 10800, 0, false), ("arcsec", angle, Math.PI / 648000, 0, false), ("″", angle, Math.PI / 648000, 0, false),
                ("rpm", angle * time.Power(-1), Math.PI / 30, 0, false), ("atm", pressure, 101325, 0, false), ("psi", pressure, 6894.757293168, 0, false),
                ("lb", mass, 0.45359237, 0, false), ("oz", mass, 0.028349523125, 0, false), ("in", length, 0.0254, 0, false), ("ft", length, 0.3048, 0, false),
                ("yd", length, 0.9144, 0, false), ("mi", length, 1609.344, 0, false), ("nmi", length, 1852, 0, false), ("NM", length, 1852, 0, false),
                ("kn", length * time.Power(-1), 1852.0 / 3600, 0, false), ("au", length, 1.495978707e11, 0, false), ("%", Dimension.None, 0.01, 0, false),
                ("one", Dimension.None, 1, 0, false), ("1", Dimension.None, 1, 0, false)
            ];

            return units.ToDictionary(unit => unit.Symbol, unit => (new Unit(unit.Symbol, unit.Dimension, unit.Factor, unit.Offset), unit.IsPrefixable), StringComparer.Ordinal);
        }

        /// <summary>
        /// Creates the names of the known units.
        /// </summary>
        /// <returns>The symbols by name, in lower case.</returns>
        private static Dictionary<string, string> CreateNames()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["metre"] = "m", ["meter"] = "m", ["gram"] = "g", ["second"] = "s", ["ampere"] = "A", ["kelvin"] = "K", ["mole"] = "mol", ["candela"] = "cd",
                ["radian"] = "rad", ["steradian"] = "sr", ["bit"] = "bit", ["byte"] = "B", ["hertz"] = "Hz", ["newton"] = "N", ["pascal"] = "Pa", ["joule"] = "J",
                ["watt hour"] = "Wh", ["watt-hour"] = "Wh", ["electronvolt"] = "eV", ["watt"] = "W", ["coulomb"] = "C", ["ampere hour"] = "Ah", ["volt"] = "V",
                ["farad"] = "F", ["ohm"] = "ohm", ["siemens"] = "S", ["weber"] = "Wb", ["tesla"] = "T", ["henry"] = "H", ["lumen"] = "lm", ["lux"] = "lx",
                ["becquerel"] = "Bq", ["gray"] = "Gy", ["sievert"] = "Sv", ["katal"] = "kat", ["litre"] = "L", ["liter"] = "L", ["tonne"] = "t", ["bar"] = "bar",
                ["degree celsius"] = "°C", ["degree fahrenheit"] = "°F", ["minute"] = "min", ["hour"] = "h", ["day"] = "d", ["year"] = "yr", ["degree"] = "deg",
                ["arcminute"] = "arcmin", ["arcsecond"] = "arcsec", ["atmosphere"] = "atm", ["pound"] = "lb", ["ounce"] = "oz", ["inch"] = "in", ["foot"] = "ft",
                ["yard"] = "yd", ["mile"] = "mi", ["nautical mile"] = "nmi", ["knot"] = "kn", ["astronomical unit"] = "au", ["percent"] = "%"
            };
        }
    }
}
