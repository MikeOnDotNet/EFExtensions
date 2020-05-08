using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EFExtensions
{
    /// <summary>
    /// Handy extensions for working with primary keys of an entity
    /// </summary>
    /// <remarks>
    /// Inspired by comments found at https://stackoverflow.com/questions/30688909/how-to-get-primary-key-value-with-entity-framework-core
    /// </remarks>
    public static class DbContextKeyExtensions
    {
        private static readonly ConcurrentDictionary<Type, IProperty[]> KeyPropertiesByEntityType = new ConcurrentDictionary<Type, IProperty[]>();

        /// <summary>
        /// Retrieves the key values on an entity in a string format, "(property Name)=[value];"
        /// </summary>
        /// <param name="entry">Entry for the entity in the DbContext</param>
        /// <returns>Semi-colon delimited string in the format of, "(property Name)=[value];"</returns>
        public static string KeyValuesAsString(this EntityEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var values = entry.KeyValuesOf();
            return $"{string.Join("; ", values.Select(v => $"({v.Key})=[{v.Value}]"))}";
        }

        /// <summary>
        /// Gets KeyValuePair of property name / value for the primary keys on an entity
        /// </summary>
        /// <param name="entry">Entry for the entity in the DbContext</param>
        /// <returns>An IEnumerable of KeyValuePairs of the property name/value for each key element</returns>
        public static IEnumerable<KeyValuePair<string, object>> KeyValuesOf(this EntityEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var keyProperties = entry.Context.GetKeyProperties(entry.Entity.GetType());

            foreach (var keyProperty in keyProperties)
            {
                yield return new KeyValuePair<string, object>(keyProperty.Name, entry.Property(keyProperty.Name).CurrentValue);
            }
        }

        /// <summary>
        /// Gets a collection of key values on an entity
        /// </summary>
        /// <typeparam name="TEntity">Entity to retrieve from</typeparam>
        /// <param name="context">DbContext the entity belongs to</param>
        /// <param name="entity">The entity to get the values from</param>
        /// <returns>An IEnumerable of objects representing the key values</returns>
        public static IEnumerable<object> KeyOf<TEntity>(this DbContext context, TEntity entity)
            where TEntity : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var entry = context.Entry(entity);
            return entry.KeyOf();
        }

        /// <summary>
        /// Gets the value of the key on an entity.  NOTE: The entity can have only 1 key
        /// </summary>
        /// <typeparam name="TEntity">Entity to retrieve from</typeparam>
        /// <typeparam name="TKey">Type of the key value to return</typeparam>
        /// <param name="context">DbContext the entity belongs to</param>
        /// <param name="entity">The entity to get the values from</param>
        /// <returns>The value of the key on the entity</returns>
        /// <exception cref="ArgumentNullException">If the entity is null</exception>
        /// <exception cref="InvalidOperationException">When there is more than 1 key on the entity</exception>
        public static TKey KeyOf<TEntity, TKey>(this DbContext context, TEntity entity)
            where TEntity : class
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var keyParts = context.KeyOf(entity).ToArray();
            if (keyParts.Length > 1)
            {
                throw new InvalidOperationException($"Key is composite and has '{keyParts.Length}' parts.");
            }

            return (TKey)keyParts[0];
        }

        /// <summary>
        /// Returns a collection of key values for the passed entry
        /// </summary>
        /// <param name="entry">Entry for the entity to retrieve the values from</param>
        /// <returns>An IEnumerable of objects representing the key values</returns>
        /// <exception cref="ArgumentNullException">When the entry passed is null</exception>
        public static IEnumerable<object> KeyOf(this EntityEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var keyProperties = entry.Context.GetKeyProperties(entry.Entity.GetType());

            return keyProperties
                .Select(property => entry.Entity.GetPropertyValue(property.Name))
                .AsEnumerable();
        }

        /// <summary>
        /// Gets the value of the key on an entity for the passed entry.  NOTE: The entity can have only 1 key
        /// </summary>
        /// <typeparam name="TKey">Type of the key value to return</typeparam>
        /// <param name="entry">The entry of the entity to get the values from</param>
        /// <returns>The value of the key on the entity</returns>
        /// <exception cref="ArgumentNullException">If the entry is null</exception>
        /// <exception cref="InvalidOperationException">When there is more than 1 key on the entity</exception>
        public static TKey KeyOf<TKey>(this EntityEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var keyParts = entry.KeyOf().ToArray();
            if (!keyParts.Any())
            {
                throw new InvalidOperationException($"Key is composite and has '{keyParts.Count()}' parts.");
            }

            return (TKey)keyParts[0];
        }

        private static IEnumerable<IProperty> GetKeyProperties(this IDbContextDependencies context, Type entityType)
        {
            var keyProperties = KeyPropertiesByEntityType.GetOrAdd(
                entityType,
                t => context.FindPrimaryKeyProperties(entityType).ToArray());
            return keyProperties;
        }

        private static IEnumerable<IProperty> FindPrimaryKeyProperties(this IDbContextDependencies dbContext, Type entityType)
        {
            return dbContext.Model.FindEntityType(entityType).FindPrimaryKey().Properties;
        }

        private static object GetPropertyValue<T>(this T entity, string propertyName)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException($"{nameof(propertyName)} must have value", nameof(propertyName));
            }

            return typeof(T).GetProperty(propertyName)?.GetValue(entity, null);
        }
    }
}